using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class AuditModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    public IReadOnlyList<SecurityAuditEvent> Events { get; private set; } = Array.Empty<SecurityAuditEvent>();
    public async Task OnGetAsync(CancellationToken cancellationToken) => Events = await db.SecurityAuditEvents.AsNoTracking().Where(x => x.UserId == users.GetUserId(User)).OrderByDescending(x => x.OccurredAtUtc).Take(100).ToListAsync(cancellationToken);
}
