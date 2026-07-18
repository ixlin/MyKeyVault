using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class EditModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.VaultItems.AsNoTracking().Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User) && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();
        Input = new InputModel { Id = item.Id, Title = item.Title, Kind = item.Kind, UrlOrHost = item.UrlOrHost, IsFavorite = item.IsFavorite, TagsInput = string.Join(", ", item.Tags.Select(x => x.Name)) };
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == Input.Id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();
        var names = Input.TagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var available = await db.VaultTags.Where(x => x.OwnerId == userId && names.Contains(x.Name)).ToListAsync(cancellationToken);
        item.Tags.Clear();
        foreach (var name in names) { var tag = available.SingleOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); if (tag is null) { tag = new VaultTag { OwnerId = userId, Name = name }; db.VaultTags.Add(tag); } item.Tags.Add(tag); }
        item.Title = Input.Title.Trim(); item.Kind = Input.Kind; item.UrlOrHost = string.IsNullOrWhiteSpace(Input.UrlOrHost) ? null : Input.UrlOrHost.Trim(); item.IsFavorite = Input.IsFavorite; item.UpdatedAtUtc = DateTime.UtcNow;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_updated", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("Details", new { id = item.Id });
    }
    public sealed class InputModel { public Guid Id { get; set; } [Required, StringLength(160)] public string Title { get; set; } = string.Empty; [Required] public VaultItemKind Kind { get; set; } [StringLength(2048)] public string? UrlOrHost { get; set; } [StringLength(500)] public string TagsInput { get; set; } = string.Empty; public bool IsFavorite { get; set; } }
}
