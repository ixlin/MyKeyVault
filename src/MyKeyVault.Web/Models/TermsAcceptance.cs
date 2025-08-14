using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyKeyVault.Web.Models;

public class TermsAcceptance
{
    [Key]
    public long Id { get; set; }

    [Required, MaxLength(64)]
    public string Version { get; set; } = "v1"; // 当前条款版本

    [Required]
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string UserId { get; set; } = default!;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}
