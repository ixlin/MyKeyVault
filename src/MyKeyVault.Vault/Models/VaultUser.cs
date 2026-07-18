using Microsoft.AspNetCore.Identity;

namespace MyKeyVault.Vault.Models;

public sealed class VaultUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
}
