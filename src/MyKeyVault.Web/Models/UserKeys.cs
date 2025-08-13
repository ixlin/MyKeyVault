using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

public class UserKeys
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = default!; // FK -> AspNetUsers

    // KDF 参数与盐（Base64）
    [Required]
    public string KdfSalt { get; set; } = string.Empty;
    public string KdfParams { get; set; } = ""; // JSON 参数（如迭代次数、内存、并行度、版本）

    // 用 KEK 包裹的 DEK（Base64/JSON）
    [Required]
    public string WrappedDEK { get; set; } = string.Empty;

    public string CryptoVersion { get; set; } = "v1";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
