using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models.Tushare;

/// <summary>
/// 认证请求
/// </summary>
public class AuthTokenRequest
{
    [Required]
    public string AppId { get; set; } = string.Empty;

    [Required]
    public string AppSecret { get; set; } = string.Empty;
}
