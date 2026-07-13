using Microsoft.AspNetCore.Identity;

namespace MyKeyVault.Web.Models;

public class ApplicationUser : IdentityUser
{
    /// <summary>微信小程序内唯一的用户标识，只用于账号绑定与登录。</summary>
    public string? WechatOpenId { get; set; }

    /// <summary>用户主动选择授权后保存的微信昵称。</summary>
    public string? WechatNickname { get; set; }

    /// <summary>用户主动选择授权后保存的头像地址。</summary>
    public string? WechatAvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // 法律条款接受时间（null 表示未接受）
    public DateTime? TermsAcceptedAt { get; set; }
}
