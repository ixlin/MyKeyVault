using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 微信公众号文章
/// </summary>
public class WechatArticle
{
    [Key]
    public long ArticleId { get; set; }

    /// <summary>
    /// 关联用户ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 原始微信链接
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// 文章唯一标识（文件夹名）
    /// </summary>
    [MaxLength(100)]
    public string? ArticleUniqueId { get; set; }

    /// <summary>
    /// 文章标题
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// 作者/公众号
    /// </summary>
    [MaxLength(100)]
    public string? Author { get; set; }

    /// <summary>
    /// 发布时间（字符串格式）
    /// </summary>
    [MaxLength(50)]
    public string? PublishTime { get; set; }

    /// <summary>
    /// HTML文件相对路径
    /// </summary>
    [MaxLength(500)]
    public string? HtmlFilePath { get; set; }

    /// <summary>
    /// 图片数量
    /// </summary>
    public int ImagesCount { get; set; } = 0;

    /// <summary>
    /// 视频数量
    /// </summary>
    public int VideosCount { get; set; } = 0;

    /// <summary>
    /// 状态: pending, processing, completed, failed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// 任务ID（关联 Python 服务的任务）
    /// </summary>
    [MaxLength(100)]
    public string? TaskId { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// 导航属性
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
