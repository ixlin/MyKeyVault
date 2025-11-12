namespace MyKeyVault.Web.Services;

/// <summary>
/// Tushare 配置选项
/// </summary>
public class TushareOptions
{
    public const string SectionName = "Tushare";

    /// <summary>
    /// 官方 Tushare API Token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Tushare API 基础地址
    /// </summary>
    public string BaseUrl { get; set; } = "http://api.tushare.pro";

    /// <summary>
    /// JWT 密钥（用于签发访问令牌）
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// JWT 过期时间（秒）
    /// </summary>
    public int JwtExpiresInSeconds { get; set; } = 7200; // 2小时

    /// <summary>
    /// 加密密钥（用于 AppSecret 加密，应与 VaultAccount 使用同样机制）
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;
}
