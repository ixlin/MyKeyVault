using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class SecurityAuditEvent
{
    public long Id { get; set; }
    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;
    public Guid? VaultItemId { get; set; }
    [Required, MaxLength(80)] public string Action { get; set; } = string.Empty;
    [MaxLength(80)] public string? Result { get; set; }
    [MaxLength(128)] public string? RequestCorrelationId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
