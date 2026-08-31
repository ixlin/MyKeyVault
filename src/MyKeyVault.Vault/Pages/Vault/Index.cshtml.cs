using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class IndexModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    public IReadOnlyList<VaultItemSummary> Items { get; private set; } = Array.Empty<VaultItemSummary>();
    public Guid? RevealedItemId { get; private set; }
    public IReadOnlyDictionary<Guid, string> RevealedValues { get; private set; } = new Dictionary<Guid, string>();

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadItemsAsync(cancellationToken);

    public async Task<IActionResult> OnPostRevealAsync(Guid id, string? query, string? group, CancellationToken cancellationToken)
    {
        Query = query;
        Group = group;
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.Include(x => x.Secrets)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();

        RevealedItemId = item.Id;
        RevealedValues = item.Secrets.ToDictionary(x => x.Id, cipher.Decrypt);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "item_secrets_revealed", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        await LoadItemsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCopyAsync(Guid id, Guid secretId, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var fieldName = await db.VaultSecrets
            .Where(x => x.Id == secretId && x.VaultItemId == id && x.VaultItem.OwnerId == userId && !x.VaultItem.IsArchived)
            .Select(x => x.FieldName)
            .SingleOrDefaultAsync(cancellationToken);
        if (fieldName is null || !VaultFieldPresentation.CanCopy(fieldName)) return NotFound();
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = id, Action = "secret_copied", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return new StatusCodeResult(StatusCodes.Status204NoContent);
    }

    private async Task LoadItemsAsync(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        IQueryable<VaultItem> items = db.VaultItems.AsNoTracking().Where(x => x.OwnerId == userId && !x.IsArchived);
        if (!string.IsNullOrWhiteSpace(Query))
        {
            var term = Query.Trim();
            items = items.Where(x => EF.Functions.ILike(x.Title, $"%{term}%") || (x.UrlOrHost != null && EF.Functions.ILike(x.UrlOrHost, $"%{term}%")) || x.Tags.Any(tag => EF.Functions.ILike(tag.Name, $"%{term}%")));
        }
        items = Group?.ToLowerInvariant() switch
        {
            "login" => items.Where(x => x.Kind == VaultItemKind.Login || x.Kind == VaultItemKind.ApiKey || x.Kind == VaultItemKind.Server || x.Kind == VaultItemKind.Database || x.Kind == VaultItemKind.BlockchainAccount),
            "card" => items.Where(x => x.Kind == VaultItemKind.BankCard || x.Kind == VaultItemKind.CreditCard),
            "note" => items.Where(x => x.Kind == VaultItemKind.SecureNote),
            _ => items,
        };

        Items = await items.OrderByDescending(x => x.IsFavorite).ThenByDescending(x => x.UpdatedAtUtc)
            .Select(x => new VaultItemSummary(
                x.Id, x.Title, x.Kind, x.UrlOrHost, x.IsFavorite, x.UpdatedAtUtc,
                x.Secrets.OrderBy(secret => secret.CreatedAtUtc).Select(secret => new SecretSummary(secret.Id, secret.FieldName)).ToList(),
                x.Tags.OrderBy(tag => tag.Name).Select(tag => tag.Name).ToList()))
            .ToListAsync(cancellationToken);
    }

    public sealed record SecretSummary(Guid Id, string FieldName);
    public sealed record VaultItemSummary(Guid Id, string Title, VaultItemKind Kind, string? UrlOrHost, bool IsFavorite, DateTime UpdatedAtUtc, IReadOnlyList<SecretSummary> Secrets, IReadOnlyList<string> Tags)
    {
        public IReadOnlyList<SecretSummary> AccountFields => Secrets.Where(x => VaultFieldPresentation.Classify(x.FieldName) == VaultFieldColumn.Account).ToList();
        public IReadOnlyList<SecretSummary> PasswordFields => Secrets.Where(x => VaultFieldPresentation.Classify(x.FieldName) == VaultFieldColumn.Password).ToList();
        public IReadOnlyList<SecretSummary> NoteFields => Secrets.Where(x => VaultFieldPresentation.Classify(x.FieldName) == VaultFieldColumn.Note).ToList();
        public string KindMark => Kind switch { VaultItemKind.BankCard => "借", VaultItemKind.CreditCard => "信", VaultItemKind.SecureNote => "记", VaultItemKind.ApiKey => "KEY", VaultItemKind.Server => "服", VaultItemKind.Database => "库", VaultItemKind.BlockchainAccount => "链", _ => "账" };
        public string KindCss => Kind switch { VaultItemKind.BankCard => "bank", VaultItemKind.CreditCard => "credit", VaultItemKind.SecureNote => "note", _ => "login" };
        public string? SafeExternalUrl => Uri.TryCreate(UrlOrHost, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? uri.AbsoluteUri : null;
    }
}
