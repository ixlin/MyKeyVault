using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

public sealed class PasswordResetService(VaultDbContext db, UserManager<VaultUser> users,
    IDataProtectionProvider protection, IPasswordResetEmailSender emailSender,
    TimeProvider clock, ILogger<PasswordResetService> logger)
{
    private const string Provider = "MyKeyVault.PasswordRecovery";
    private const string TokenName = "EmailCode.v1";
    public const string SentMessage = "如果该邮箱已注册，验证码将发送到该邮箱，请检查收件箱和垃圾邮件。验证码 10 分钟内有效。";
    public const string InvalidMessage = "验证码错误、已过期或尝试次数过多，请重新获取验证码。";
    public bool IsConfigured => emailSender.IsConfigured;

    public async Task<string> SendAsync(string email, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return "邮件服务尚未配置，暂时无法发送验证码，请联系管理员。";
        var started = Stopwatch.GetTimestamp();
        var normalized = users.NormalizeEmail(email.Trim());
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Shared database lock covers all app instances and survives app restarts.
        await LockAsync(normalized, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (user is not null && user.EmailConfirmed && user.Email is not null)
        {
            var token = await FindTokenAsync(user.Id, cancellationToken);
            var previous = Read(user.Id, token?.Value);
            var now = clock.GetUtcNow();
            var sameWindow = previous is not null && previous.WindowStart > now.AddHours(-1);
            var throttled = previous is not null && (previous.SentAt > now.AddSeconds(-60)
                || (sameWindow && (previous.SendCount >= 5 || previous.WindowFailures >= 10)));
            if (!throttled)
            {
                var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6");
                var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var challenge = new Challenge(now, now.AddMinutes(10), sameWindow ? previous!.WindowStart : now,
                    sameWindow ? previous!.SendCount + 1 : 1, sameWindow ? previous!.WindowFailures : 0,
                    0, salt, Hash(salt, code), await users.GeneratePasswordResetTokenAsync(user));
                // Persist only after SMTP accepts the message. Failure leaves the old code intact.
                try { await emailSender.SendCodeAsync(user.Email, code, cancellationToken); }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("Password reset mail delivery failed ({FailureType}).", ex.GetType().Name);
                    // Do not reveal account existence through a different SMTP-failure response.
                    var delay = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(started);
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
                    return SentMessage;
                }
                Store(user.Id, token, challenge);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
        // Same response and minimum time for unknown accounts / rate-limited addresses.
        var remaining = TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
        return SentMessage;
    }

    public async Task<IdentityResult> ResetAsync(string email, string code, string password, CancellationToken cancellationToken)
    {
        var normalized = users.NormalizeEmail(email.Trim());
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockAsync(normalized, cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, cancellationToken);
        if (user is null || !user.EmailConfirmed) return Invalid();
        var token = await FindTokenAsync(user.Id, cancellationToken);
        var challenge = Read(user.Id, token?.Value);
        if (challenge is null || challenge.ExpiresAt <= clock.GetUtcNow() || challenge.Attempts >= 5
            || challenge.WindowFailures >= 10 || string.IsNullOrEmpty(challenge.ResetToken)) return Invalid();
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(challenge.CodeHash),
                Convert.FromHexString(Hash(challenge.Salt, code))))
        {
            Store(user.Id, token, challenge with { Attempts = challenge.Attempts + 1, WindowFailures = challenge.WindowFailures + 1 });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Invalid();
        }
        var result = await users.ResetPasswordAsync(user, challenge.ResetToken, password);
        if (!result.Succeeded) return result;
        // Password hash, security stamp, unlock, one-time consumption and audit commit together.
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        Store(user.Id, token, challenge with { ResetToken = "", CodeHash = "", ExpiresAt = clock.GetUtcNow() });
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = user.Id, Action = "password_reset", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        try { await emailSender.SendChangedNoticeAsync(user.Email!, cancellationToken); }
        catch (Exception ex) { logger.LogWarning("Password changed notification failed ({FailureType}).", ex.GetType().Name); }
        return IdentityResult.Success;
    }

    private Task LockAsync(string normalized, CancellationToken cancellationToken) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({Provider + normalized}, 0))", cancellationToken);

    private Task<IdentityUserToken<string>?> FindTokenAsync(string userId, CancellationToken cancellationToken) =>
        db.Set<IdentityUserToken<string>>().SingleOrDefaultAsync(x => x.UserId == userId && x.LoginProvider == Provider && x.Name == TokenName, cancellationToken);

    private Challenge? Read(string userId, string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try { return JsonSerializer.Deserialize<Challenge>(Protector(userId).Unprotect(value)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }

    private void Store(string userId, IdentityUserToken<string>? token, Challenge challenge)
    {
        if (token is null)
        {
            token = new IdentityUserToken<string> { UserId = userId, LoginProvider = Provider, Name = TokenName };
            db.Add(token);
        }
        token.Value = Protector(userId).Protect(JsonSerializer.Serialize(challenge));
    }

    private IDataProtector Protector(string userId) => protection.CreateProtector(Provider, TokenName, userId);
    private static string Hash(string salt, string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(salt + ":" + code)));
    private static IdentityResult Invalid() => IdentityResult.Failed(new IdentityError { Code = "InvalidCode", Description = InvalidMessage });
    private sealed record Challenge(DateTimeOffset SentAt, DateTimeOffset ExpiresAt, DateTimeOffset WindowStart,
        int SendCount, int WindowFailures, int Attempts, string Salt, string CodeHash, string ResetToken);
}
