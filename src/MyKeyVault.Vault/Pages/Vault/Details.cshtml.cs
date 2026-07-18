using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class DetailsModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    public ItemDetails? Item { get; private set; }
    public Guid? RevealedSecretId { get; private set; }
    public string? RevealedValue { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) { await LoadAsync(id, cancellationToken); return Item is null ? NotFound() : Page(); }
    public async Task<IActionResult> OnPostRevealAsync(Guid id, Guid secretId, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.Include(x => x.Secrets).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        var secret = item?.Secrets.SingleOrDefault(x => x.Id == secretId);
        if (item is null || secret is null) return NotFound();
        RevealedValue = cipher.Decrypt(secret); RevealedSecretId = secret.Id;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "secret_revealed", Result = "success" });
        await db.SaveChangesAsync(cancellationToken); Item = ItemDetails.From(item); return Page();
    }
    public async Task<IActionResult> OnPostArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();
        item.IsArchived = true; item.UpdatedAtUtc = DateTime.UtcNow;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_archived", Result = "success" });
        await db.SaveChangesAsync(cancellationToken); return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostCopyAsync(Guid id, Guid secretId, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var allowed = await db.VaultSecrets.AnyAsync(x => x.Id == secretId && x.VaultItemId == id && x.VaultItem.OwnerId == userId && !x.VaultItem.IsArchived, cancellationToken);
        if (!allowed) return NotFound();
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = id, Action = "secret_copied", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return new StatusCodeResult(StatusCodes.Status204NoContent);
    }
    private async Task LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.AsNoTracking().Include(x => x.Secrets).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        Item = item is null ? null : ItemDetails.From(item);
    }
    public sealed record SecretSummary(Guid Id, string FieldName);
    public sealed record ItemDetails(Guid Id, string Title, VaultItemKind Kind, string? UrlOrHost, IReadOnlyList<SecretSummary> Secrets)
    {
        public string KindLabel => Kind switch { VaultItemKind.ApiKey => "API KEY", VaultItemKind.BlockchainAccount => "链上账户", VaultItemKind.SecureNote => "私密笔记", VaultItemKind.Login => "账号登录", _ => Kind.ToString().ToUpperInvariant() };
        public static ItemDetails From(VaultItem item) => new(item.Id, item.Title, item.Kind, item.UrlOrHost, item.Secrets.Select(x => new SecretSummary(x.Id, x.FieldName)).ToList());
    }
}
