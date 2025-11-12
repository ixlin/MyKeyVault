namespace MyKeyVault.Web.Models.Tushare;

/// <summary>
/// 认证响应
/// </summary>
public class AuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; } // 秒
    public string TokenType { get; set; } = "Bearer";
}
