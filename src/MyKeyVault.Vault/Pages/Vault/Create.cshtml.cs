using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class CreateModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var userId = users.GetUserId(User)!;
        var item = new VaultItem { OwnerId = userId, Title = Input.Title.Trim(), Kind = Input.Kind, UrlOrHost = string.IsNullOrWhiteSpace(Input.UrlOrHost) ? null : Input.UrlOrHost.Trim(), IsFavorite = Input.IsFavorite };
        var tagNames = Input.TagsInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingTags = await db.VaultTags.Where(x => x.OwnerId == userId && tagNames.Contains(x.Name)).ToListAsync(cancellationToken);
        foreach (var tagName in tagNames)
        {
            var tag = existingTags.SingleOrDefault(x => string.Equals(x.Name, tagName, StringComparison.OrdinalIgnoreCase));
            if (tag is null) { tag = new VaultTag { OwnerId = userId, Name = tagName }; db.VaultTags.Add(tag); }
            item.Tags.Add(tag);
        }
        item.Secrets.Add(cipher.Encrypt(item.Id, Input.FieldName.Trim(), Input.SecretValue));
        db.VaultItems.Add(item);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_created", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required] public VaultItemKind Kind { get; set; } = VaultItemKind.Login;
        [StringLength(2048)] public string? UrlOrHost { get; set; }
        [StringLength(500)] public string TagsInput { get; set; } = string.Empty;
        [Required, StringLength(80)] public string FieldName { get; set; } = string.Empty;
        [Required, StringLength(16_000)] public string SecretValue { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }
}
