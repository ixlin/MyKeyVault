using System.ComponentModel.DataAnnotations;
using MyKeyVault.Web.Validation;

namespace MyKeyVault.Web.Models;

public class VaultAccount
{
    [Key]
    public long AccountId { get; set; }

    [Required]
    public string UserId { get; set; } = default!; // FK -> AspNetUsers

    // Title 明文
    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    // 账号与密码（纯文本，已取消加密流程）
    public string AccountNameEncrypted { get; set; } = string.Empty;
    public string PasswordEncrypted { get; set; } = string.Empty;

    // URL 明文
    [MaxLength(255)]
    public string? Url { get; set; }

    public string? NoteEncrypted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? ConcurrencyStamp { get; set; }

    public ICollection<AccountTag> AccountTags { get; set; } = new List<AccountTag>();
}
