using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

/// <summary>Only a one-way hash is stored. The raw token is shown once at creation.</summary>
public sealed class McpAccessToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(450)] public string OwnerId { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(16)] public string Prefix { get; set; } = string.Empty;
    [Required] public byte[] TokenHash { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}
