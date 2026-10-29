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
    public string? Email { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public IReadOnlyList<RecentVaultItem> RecentItems { get; private set; } = Array.Empty<RecentVaultItem>();
    public IReadOnlyList<RecentItem> Recent { get; private set; } = Array.Empty<RecentItem>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true) return;
        var ownerId = users.GetUserId(User)!;
        var user = await users.GetUserAsync(User);
        Email = user?.Email;
        LastLoginAtUtc = user?.LastLoginAtUtc;

        VaultCount = await db.VaultItems.CountAsync(x => x.OwnerId == ownerId && !x.IsArchived, cancellationToken);
        ArticleCount = await db.KnowledgeArticles.CountAsync(x => x.OwnerId == ownerId && x.Status == "completed", cancellationToken);
        ExtractionCount = await db.ArticleExtractions.CountAsync(x => x.OwnerId == ownerId && x.Status == "completed", cancellationToken);
        RecentItems = await db.VaultItems.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && !x.IsArchived)
            .OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.UpdatedAtUtc).Take(4)
            .Select(x => new RecentVaultItem(x.Id, x.Title, x.Kind, x.UpdatedAtUtc)).ToListAsync(cancellationToken);
        Recent = await db.KnowledgeArticles.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(4)
            .Select(x => new RecentItem(x.Id, x.Title ?? "等待抓取", x.Author, x.Status, x.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    public sealed record RecentVaultItem(Guid Id, string Title, VaultItemKind Kind, DateTime UpdatedAtUtc)
    {
        public string KindMark => Kind switch { VaultItemKind.BankCard => "借", VaultItemKind.CreditCard => "信", VaultItemKind.SecureNote => "记", VaultItemKind.ApiKey => "KEY", VaultItemKind.Server => "服", VaultItemKind.Database => "库", VaultItemKind.BlockchainAccount => "链", _ => "账" };
    }

    public sealed record RecentItem(long Id, string Title, string? Author, string Status, DateTime CreatedAtUtc);
}
