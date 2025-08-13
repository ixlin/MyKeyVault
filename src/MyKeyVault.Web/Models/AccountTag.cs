using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Web.Models;

public class AccountTag
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }
    public long TagId { get; set; }

    public VaultAccount? Account { get; set; }
    public Tag? Tag { get; set; }
}
