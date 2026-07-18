using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class IndexModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    public IReadOnlyList<VaultItemSummary> Items { get; private set; } = Array.Empty<VaultItemSummary>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        IQueryable<VaultItem> query = db.VaultItems.AsNoTracking().Where(x => x.OwnerId == userId && !x.IsArchived).Include(x => x.Tags);
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var term = Query.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Title, $"%{term}%") || (x.UrlOrHost != null && EF.Functions.ILike(x.UrlOrHost, $"%{term}%")) || x.Tags.Any(tag => EF.Functions.ILike(tag.Name, $"%{term}%")));
        }
        Items = await query
            .OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.UpdatedAtUtc)
            .Select(x => new VaultItemSummary(x.Id, x.Title, x.Kind, x.UrlOrHost, x.IsFavorite, x.UpdatedAtUtc, x.Secrets.Count, x.Tags.Select(tag => tag.Name).ToList()))
            .ToListAsync(cancellationToken);
    }

    public sealed record VaultItemSummary(Guid Id, string Title, VaultItemKind Kind, string? UrlOrHost, bool IsFavorite, DateTime UpdatedAtUtc, int SecretFieldCount, IReadOnlyList<string> Tags)
    {
        public string KindLabel => Kind switch { VaultItemKind.ApiKey => "API Key", VaultItemKind.BlockchainAccount => "链上账户", VaultItemKind.SecureNote => "私密笔记", VaultItemKind.Login => "账号登录", _ => Kind.ToString() };
    }
}
