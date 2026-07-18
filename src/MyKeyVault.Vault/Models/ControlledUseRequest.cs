using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public enum ControlledUseRequestStatus { Pending, Approved, Rejected, RotationConfirmed, Expired }

/// <summary>A request to use a vault item. It deliberately contains no secret material.</summary>
public sealed class ControlledUseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(450)] public string OwnerId { get; set; } = string.Empty;
    public Guid VaultItemId { get; set; }
    [Required, MaxLength(120)] public string RequestedBy { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string RequestedAction { get; set; } = string.Empty;
    [MaxLength(500)] public string? Reason { get; set; }
    public ControlledUseRequestStatus Status { get; set; } = ControlledUseRequestStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(10);
    public DateTime? ResolvedAtUtc { get; set; }
}
