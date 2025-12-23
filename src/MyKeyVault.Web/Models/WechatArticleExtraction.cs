using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyKeyVault.Web.Models;

/// <summary>
/// 微信文章萃取记录
/// </summary>
public class WechatArticleExtraction
{
    [Key]
    public long ExtractionId { get; set; }

    /// <summary>
    /// 关联文章ID
    /// </summary>
    [Required]
    public long ArticleId { get; set; }

    /// <summary>
    /// 关联用户ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// 用户输入的提示词
    /// </summary>
    [Required]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// AI 返回的结果（Markdown格式）
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// 萃取结果文件路径
    /// </summary>
    [MaxLength(300)]
    public string? FilePath { get; set; }

    /// <summary>
    /// 使用的模型
    /// </summary>
    [MaxLength(100)]
    public string? ModelUsed { get; set; }

    /// <summary>
    /// Token消耗（可选）
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// 状态: processing, completed, failed
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "processing";

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
    /// 关联的文章
    /// </summary>
    [ForeignKey(nameof(ArticleId))]
    public WechatArticle? Article { get; set; }
}
