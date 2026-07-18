using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class SecretEditModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    [BindProperty] public Guid ItemId { get; set; }
    [BindProperty] public Guid? SecretId { get; set; }
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, Guid? secretId, CancellationToken cancellationToken)
    {
        var item = await db.VaultItems.AsNoTracking().Include(x => x.Secrets).SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User) && !x.IsArchived, cancellationToken);
        if (item is null || (secretId is not null && item.Secrets.All(x => x.Id != secretId))) return NotFound();
        ItemId = id; SecretId = secretId; Input.FieldName = secretId is null ? string.Empty : item.Secrets.Single(x => x.Id == secretId).FieldName; return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems.Include(x => x.Secrets).SingleOrDefaultAsync(x => x.Id == ItemId && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        var oldSecret = SecretId is null ? null : item?.Secrets.SingleOrDefault(x => x.Id == SecretId);
        if (item is null || (SecretId is not null && oldSecret is null)) return NotFound();
        var fieldName = Input.FieldName.Trim();
        if (item.Secrets.Any(x => x.Id != SecretId && string.Equals(x.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))) { ModelState.AddModelError("Input.FieldName", "该字段名称已存在。"); return Page(); }
        var encrypted = cipher.Encrypt(item.Id, fieldName, Input.SecretValue);
        if (oldSecret is null)
        {
            item.Secrets.Add(encrypted);
        }
        else
        {
            oldSecret.FieldName = encrypted.FieldName;
            oldSecret.Ciphertext = encrypted.Ciphertext;
            oldSecret.Nonce = encrypted.Nonce;
            oldSecret.AuthenticationTag = encrypted.AuthenticationTag;
            oldSecret.WrappedDataKey = encrypted.WrappedDataKey;
            oldSecret.KeyWrapNonce = encrypted.KeyWrapNonce;
            oldSecret.KeyWrapAuthenticationTag = encrypted.KeyWrapAuthenticationTag;
            oldSecret.EncryptionVersion = encrypted.EncryptionVersion;
            oldSecret.UpdatedAtUtc = DateTime.UtcNow;
        }
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = oldSecret is null ? "secret_created" : "secret_updated", Result = "success" });
        await db.SaveChangesAsync(cancellationToken); return RedirectToPage("Details", new { id = item.Id });
    }
    public sealed class InputModel { [Required, StringLength(80)] public string FieldName { get; set; } = string.Empty; [Required, StringLength(16_000)] public string SecretValue { get; set; } = string.Empty; }
}
