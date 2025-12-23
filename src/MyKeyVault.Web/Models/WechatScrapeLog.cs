using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 微信抓取操作日志（用于审计）
/// </summary>
public class WechatScrapeLog
{
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// 操作人 ID
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// 动作名称（如 CreateTask, DeleteTask, CancelTask）
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 操作对象 ID（文章 ID 或 Task ID）
    /// </summary>
    [MaxLength(128)]
    public string? TargetId { get; set; }

    /// <summary>
    /// 详细信息（JSON 格式，记录操作详情）
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 执行结果（Success/Failed/Partial）
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Success";

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 操作 IP
    /// </summary>
    [MaxLength(50)]
    public string? ClientIp { get; set; }

    /// <summary>
    /// 操作时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
