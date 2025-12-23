namespace MyKeyVault.Web.Models;

/// <summary>
/// PPT 生成进度信息
/// </summary>
public class PptGenerationProgress
{
    /// <summary>
    /// 进度百分比（0-100）
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>
    /// 当前步骤描述
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 状态：processing, completed, failed
    /// </summary>
    public string Status { get; set; } = "processing";

    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 下载路径（完成后）
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// 文件名
    /// </summary>
    public string? FileName { get; set; }
}
