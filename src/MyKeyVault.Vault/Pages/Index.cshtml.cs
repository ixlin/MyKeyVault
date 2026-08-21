using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages;

public sealed class IndexModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    public int VaultCount { get; private set; }
    public int ArticleCount { get; private set; }
    public int ExtractionCount { get; private set; }
    public IReadOnlyList<RecentItem> Recent { get; private set; } = Array.Empty<RecentItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true) return;
        var ownerId = users.GetUserId(User)!;
        VaultCount = await db.VaultItems.CountAsync(x => x.OwnerId == ownerId && !x.IsArchived, cancellationToken);
        ArticleCount = await db.KnowledgeArticles.CountAsync(x => x.OwnerId == ownerId && x.Status == "completed", cancellationToken);
        ExtractionCount = await db.ArticleExtractions.CountAsync(x => x.OwnerId == ownerId && x.Status == "completed", cancellationToken);
        Recent = await db.KnowledgeArticles.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(4)
            .Select(x => new RecentItem(x.Id, x.Title ?? "等待抓取", x.Author, x.Status, x.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    public sealed record RecentItem(long Id, string Title, string? Author, string Status, DateTime CreatedAtUtc);
}
