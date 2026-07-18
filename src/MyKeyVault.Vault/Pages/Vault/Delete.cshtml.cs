using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class DeleteModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    public Guid Id { get; private set; } public string Title { get; private set; } = string.Empty;
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) { var item = await db.VaultItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User), cancellationToken); if (item is null) return NotFound(); Id = item.Id; Title = item.Title; return Page(); }
    public async Task<IActionResult> OnPostAsync(Guid id, string confirmation, CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmation, "删除", StringComparison.Ordinal)) return RedirectToPage(new { id });
        var userId = users.GetUserId(User)!; var item = await db.VaultItems.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, cancellationToken); if (item is null) return NotFound();
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_deleted", Result = "success" }); db.VaultItems.Remove(item); await db.SaveChangesAsync(cancellationToken); return RedirectToPage("Index");
    }
}
