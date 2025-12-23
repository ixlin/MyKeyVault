using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

/// <summary>
/// AI 配置（用户级别）
/// </summary>
public class AIConfig
{
    [Key]
    public long ConfigId { get; set; }

    /// <summary>
    /// 关联用户ID
    /// </summary>
    [Required]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// AI 提供商: deepseek, openai, azure, claude等
    /// </summary>
    [MaxLength(50)]
    public string Provider { get; set; } = "deepseek";

    /// <summary>
    /// API Key
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API Base URL
    /// </summary>
    [MaxLength(200)]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 模型名称
    /// </summary>
    [MaxLength(100)]
    public string ModelName { get; set; } = "deepseek-chat";

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
