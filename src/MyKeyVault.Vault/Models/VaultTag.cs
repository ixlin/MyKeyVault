using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class VaultTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(450)] public string OwnerId { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string Name { get; set; } = string.Empty;
    public ICollection<VaultItem> VaultItems { get; set; } = new List<VaultItem>();
}
