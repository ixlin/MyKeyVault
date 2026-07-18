using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

public sealed class McpTokenService(VaultDbContext db)
{
    public McpAccessToken Create(string ownerId, string name, out string rawToken)
    {
        rawToken = "mkv_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return new McpAccessToken { OwnerId = ownerId, Name = name.Trim(), Prefix = rawToken[..12], TokenHash = hash };
    }

    public async Task<McpAccessToken?> ValidateAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 200) return null;
        if (!rawToken.StartsWith("mkv_", StringComparison.Ordinal) || rawToken.Length < 12) return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var prefix = rawToken[..12];
        var candidates = await db.McpAccessTokens.Where(x => x.Prefix == prefix && x.RevokedAtUtc == null && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > DateTime.UtcNow)).ToListAsync(cancellationToken);
        var token = candidates.SingleOrDefault(x => CryptographicOperations.FixedTimeEquals(x.TokenHash, hash));
        if (token is not null) token.LastUsedAtUtc = DateTime.UtcNow;
        return token;
    }
}
