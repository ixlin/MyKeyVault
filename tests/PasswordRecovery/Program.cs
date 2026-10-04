using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

// Only a disposable PostgreSQL database is allowed: never point this at production.
var connection = Environment.GetEnvironmentVariable("RECOVERY_TEST_DB") ?? throw new Exception("RECOVERY_TEST_DB required.");
if (!new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.StartsWith("recovery_test")) throw new Exception("Test database required.");
var services = new ServiceCollection();
var clock = new TestClock();
var mail = new TestMail();
services.AddLogging();
services.AddDataProtection();
services.AddSingleton<TimeProvider>(clock);
services.AddSingleton<IPasswordResetEmailSender>(mail);
services.AddDbContext<VaultDbContext>(options => options.UseNpgsql(connection));
services.AddIdentityCore<VaultUser>(options => { options.Password.RequiredLength = 12; options.User.RequireUniqueEmail = true; })
    .AddEntityFrameworkStores<VaultDbContext>().AddDefaultTokenProviders();
services.AddScoped<PasswordResetService>();
using var provider = services.BuildServiceProvider();
using (var scope = provider.CreateScope()) await scope.ServiceProvider.GetRequiredService<VaultDbContext>().Database.EnsureCreatedAsync();
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); checks++; Console.WriteLine("PASS " + name); }
async Task<string> Create(string name)
{
    using var scope = provider.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<VaultUser>>();
    var email = name + "@example.test";
    var user = new VaultUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true, LockoutEnd = DateTimeOffset.UtcNow.AddHours(1), AccessFailedCount = 3 };
    if (!(await users.CreateAsync(user, "Old-Password7!" )).Succeeded) throw new Exception("Fixture failed");
    return email;
}
async Task<string> Send(string email)
{
    using var scope = provider.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<PasswordResetService>().SendAsync(email, default);
}
async Task<IdentityResult> Reset(string email, string code, string password = "New-Password8!")
{
    using var scope = provider.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<PasswordResetService>().ResetAsync(email, code, password, default);
}
var email = await Create("successful");
Check(await Send(email) == await Send("nonexistent@example.test"), "unknown email gets same response");
var code = mail.Codes[email];
await Send(email);
Check(mail.Deliveries == 1, "60 second resend cooldown");
Check(!(await Reset(email, code, "weak")).Succeeded, "weak password rejected");
Check((await Reset(email, code)).Succeeded, "correct code resets password");
Check(!(await Reset(email, code)).Succeeded, "consumed code cannot be reused");
using (var scope = provider.CreateScope())
{
    var users = scope.ServiceProvider.GetRequiredService<UserManager<VaultUser>>();
    var user = (await users.FindByEmailAsync(email))!;
    Check(await users.CheckPasswordAsync(user, "New-Password8!") && !await users.CheckPasswordAsync(user, "Old-Password7!"), "new password valid, old password invalid");
    Check(user.LockoutEnabled && user.LockoutEnd is null && user.AccessFailedCount == 0, "unlocks account but preserves lockout protection");
    Check(await scope.ServiceProvider.GetRequiredService<VaultDbContext>().SecurityAuditEvents.CountAsync() == 1, "successful reset audited once");
}
email = await Create("expired"); await Send(email); code = mail.Codes[email]; clock.Advance(TimeSpan.FromMinutes(11));
Check(!(await Reset(email, code)).Succeeded, "expired code rejected");
email = await Create("attempts"); await Send(email); code = mail.Codes[email];
for (var i = 0; i < 5; i++) Check(!(await Reset(email, code == "000000" ? "111111" : "000000")).Succeeded, "wrong attempt " + (i + 1));
Check(!(await Reset(email, code)).Succeeded, "five wrong attempts invalidate correct code");
email = await Create("replacement"); await Send(email); code = mail.Codes[email]; clock.Advance(TimeSpan.FromMinutes(2)); await Send(email);
Check(!(await Reset(email, code)).Succeeded, "resend invalidates old code");
Check((await Reset(email, mail.Codes[email])).Succeeded, "replacement code valid");
email = await Create("concurrency"); await Send(email); code = mail.Codes[email];
var concurrent = await Task.WhenAll(Reset(email, code), Reset(email, code));
Check(concurrent.Count(x => x.Succeeded) == 1, "concurrent reset consumes code exactly once");
email = await Create("rate");
var before = mail.Deliveries;
for (var i = 0; i < 6; i++) { await Send(email); clock.Advance(TimeSpan.FromMinutes(2)); }
Check(mail.Deliveries - before == 5, "five sends per hour cap");
email = await Create("smtpfailed"); mail.Fail = true; await Send(email); mail.Fail = false;
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();
    var user = await db.Users.SingleAsync(x => x.Email == email);
    Check(!await db.Set<IdentityUserToken<string>>().AnyAsync(x => x.UserId == user.Id), "SMTP failure does not persist usable challenge");
}
mail.Configured = false;
Check((await Send(email)).Contains("尚未配置"), "unconfigured SMTP clearly reported");
Console.WriteLine($"{checks} checks passed. No production data used.");

sealed class TestClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan amount) => now += amount;
}
sealed class TestMail : IPasswordResetEmailSender
{
    public bool Configured = true;
    public bool Fail;
    public bool IsConfigured => Configured;
    public int Deliveries;
    public Dictionary<string, string> Codes = new();
    public Task SendCodeAsync(string email, string code, CancellationToken cancellationToken)
    {
        if (Fail) throw new IOException("Synthetic SMTP failure");
        Codes[email] = code; Deliveries++; return Task.CompletedTask;
    }
    public Task SendChangedNoticeAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;
}
