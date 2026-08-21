using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class ArticleAiSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(450)] public string OwnerId { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string Provider { get; set; } = "deepseek";
    [Required, MaxLength(300)] public string BaseUrl { get; set; } = "https://api.deepseek.com";
    [Required, MaxLength(120)] public string ModelName { get; set; } = "deepseek-chat";
    [Required] public byte[] ApiKeyCiphertext { get; set; } = Array.Empty<byte>();
    [Required] public byte[] ApiKeyNonce { get; set; } = Array.Empty<byte>();
    [Required] public byte[] ApiKeyAuthenticationTag { get; set; } = Array.Empty<byte>();
    [Required] public byte[] WrappedDataKey { get; set; } = Array.Empty<byte>();
    [Required] public byte[] KeyWrapNonce { get; set; } = Array.Empty<byte>();
    [Required] public byte[] KeyWrapAuthenticationTag { get; set; } = Array.Empty<byte>();
    [Required, MaxLength(32)] public string EncryptionVersion { get; set; } = "aes-256-gcm-v1";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
