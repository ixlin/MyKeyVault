using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class AuditModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    public IReadOnlyList<SecurityAuditEvent> Events { get; private set; } = Array.Empty<SecurityAuditEvent>();
    public IReadOnlyDictionary<Guid, string> ItemTitles { get; private set; } = new Dictionary<Guid, string>();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        TotalCount = await db.SecurityAuditEvents.CountAsync(x => x.UserId == userId, cancellationToken);
        Events = await db.SecurityAuditEvents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        var itemIds = Events.Where(x => x.VaultItemId is not null).Select(x => x.VaultItemId!.Value).Distinct().ToList();
        if (itemIds.Count > 0)
        {
            ItemTitles = await db.VaultItems.AsNoTracking()
                .Where(x => x.OwnerId == userId && itemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);
        }
    }
}
