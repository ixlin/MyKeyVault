using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

/// <summary>
/// Tushare 应用凭证（AppId + AppSecret）
/// </summary>
public class TushareApp
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// 加密存储的 AppSecret
    /// </summary>
    [Required]
    public string EncryptedSecret { get; set; } = string.Empty;

    /// <summary>
    /// 所属用户（可为空表示系统级）
    /// </summary>
    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "active"; // active, disabled

    /// <summary>
    /// 应用备注说明（用途、分配对象等）
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// 最后一次调用时间
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// 累计调用次数
    /// </summary>
    public long CallCount { get; set; } = 0;

    /// <summary>
    /// 软删除标记
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 删除时间
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
