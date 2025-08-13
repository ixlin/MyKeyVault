using Microsoft.AspNetCore.Identity;

namespace MyKeyVault.Web.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // 法律条款接受时间（null 表示未接受）
    public DateTime? TermsAcceptedAt { get; set; }
}
