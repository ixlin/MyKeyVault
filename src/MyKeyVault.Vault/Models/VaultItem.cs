using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public enum VaultItemKind
{
    Login,
    ApiKey,
    Server,
    Database,
    BlockchainAccount,
    SecureNote
}

public sealed class VaultItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Title { get; set; } = string.Empty;

    public VaultItemKind Kind { get; set; }

    [MaxLength(2048)]
    public string? UrlOrHost { get; set; }

    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<VaultSecret> Secrets { get; set; } = new List<VaultSecret>();
    public ICollection<VaultTag> Tags { get; set; } = new List<VaultTag>();
}
