using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

/// <summary>
/// Tushare API 调用日志
/// </summary>
public class CallLog
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string AppId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ApiName { get; set; } = string.Empty;

    /// <summary>
    /// 参数的 Hash（用于去重和统计）
    /// </summary>
    [MaxLength(64)]
    public string ParamsHash { get; set; } = string.Empty;

    /// <summary>
    /// 原始参数 JSON
    /// </summary>
    public string ParamsJson { get; set; } = string.Empty;

    public DateTime RequestAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 请求耗时（毫秒）
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// HTTP 状态码或自定义状态
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 请求唯一标识
    /// </summary>
    [MaxLength(64)]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// 错误信息（若有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
