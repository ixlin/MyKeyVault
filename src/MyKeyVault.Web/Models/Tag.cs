using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

public class Tag
{
    [Key]
    public long TagId { get; set; }

    [Required]
    public string UserId { get; set; } = default!; // FK

    [MaxLength(50)]
    [Required]
    public string TagName { get; set; } = string.Empty; // 明文（按确认）

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AccountTag> AccountTags { get; set; } = new List<AccountTag>();
}
