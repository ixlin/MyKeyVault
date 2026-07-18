using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class VaultSecret
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VaultItemId { get; set; }
    public VaultItem VaultItem { get; set; } = default!;

    [Required, MaxLength(80)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    public byte[] Ciphertext { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] AuthenticationTag { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] WrappedDataKey { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] KeyWrapNonce { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] KeyWrapAuthenticationTag { get; set; } = Array.Empty<byte>();

    [Required, MaxLength(32)]
    public string EncryptionVersion { get; set; } = "aes-256-gcm-v1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
