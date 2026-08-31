using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class EditModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    private static readonly string[] CardFieldTemplates = ["卡号", "持卡人", "有效期", "安全码 CVV/CVC", "PIN / 取款密码"];
    [BindProperty] public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.VaultItems.AsNoTracking()
            .Include(x => x.Tags)
            .Include(x => x.Secrets)
            .SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User) && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();

        var secrets = item.Secrets.OrderBy(x => x.CreatedAtUtc)
            .Select(x => new SecretInputModel { Id = x.Id, FieldName = x.FieldName, Value = cipher.Decrypt(x) })
            .ToList();
        if (item.Kind is VaultItemKind.BankCard or VaultItemKind.CreditCard)
        {
            foreach (var fieldName in CardFieldTemplates.Where(fieldName => secrets.All(x => !string.Equals(x.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))))
                secrets.Add(new SecretInputModel { FieldName = fieldName });
        }

        Input = new InputModel
        {
            Id = item.Id,
            Title = item.Title,
            Kind = item.Kind,
            UrlOrHost = item.UrlOrHost,
            IsFavorite = item.IsFavorite,
            TagsInput = string.Join(", ", item.Tags.OrderBy(x => x.Name).Select(x => x.Name)),
            Secrets = secrets
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();

        var userId = users.GetUserId(User)!;
        var item = await db.VaultItems
            .Include(x => x.Tags)
            .Include(x => x.Secrets)
            .SingleOrDefaultAsync(x => x.Id == Input.Id && x.OwnerId == userId && !x.IsArchived, cancellationToken);
        if (item is null) return NotFound();

        var postedIds = Input.Secrets.Where(x => x.Id != Guid.Empty).Select(x => x.Id).ToList();
        if (postedIds.Count != postedIds.Distinct().Count() || item.Secrets.Any(x => !postedIds.Contains(x.Id)) || postedIds.Any(x => item.Secrets.All(secret => secret.Id != x)))
        {
            ModelState.AddModelError(string.Empty, "加密字段已经发生变化，请返回密码本后重新编辑。");
            return Page();
        }

        var additions = Input.Secrets.Where(x => x.Id == Guid.Empty && !string.IsNullOrWhiteSpace(x.Value)).ToList();
        if (additions.Any(x => Input.Kind is not (VaultItemKind.BankCard or VaultItemKind.CreditCard) || !CardFieldTemplates.Contains(x.FieldName, StringComparer.OrdinalIgnoreCase))
            || additions.GroupBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
        {
            ModelState.AddModelError(string.Empty, "新增的信用卡字段无效，请返回密码本后重新编辑。");
            return Page();
        }

        foreach (var secret in item.Secrets)
        {
            var index = Input.Secrets.FindIndex(x => x.Id == secret.Id);
            if (index < 0 || string.IsNullOrWhiteSpace(Input.Secrets[index].Value))
                ModelState.AddModelError($"Input.Secrets[{Math.Max(index, 0)}].Value", $"{secret.FieldName}不能为空。");
        }
        if (!ModelState.IsValid) return Page();

        var names = (Input.TagsInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length is > 0 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var available = await db.VaultTags.Where(x => x.OwnerId == userId && names.Contains(x.Name)).ToListAsync(cancellationToken);
        item.Tags.Clear();
        foreach (var name in names)
        {
            var tag = available.SingleOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (tag is null)
            {
                tag = new VaultTag { OwnerId = userId, Name = name };
                db.VaultTags.Add(tag);
            }
            item.Tags.Add(tag);
        }

        foreach (var secret in item.Secrets)
        {
            var posted = Input.Secrets.Single(x => x.Id == secret.Id);
            cipher.Update(secret, posted.Value!);
        }
        foreach (var addition in additions)
            item.Secrets.Add(cipher.Encrypt(item.Id, addition.FieldName, addition.Value!.Trim()));

        item.Title = Input.Title.Trim();
        item.Kind = Input.Kind;
        item.UrlOrHost = string.IsNullOrWhiteSpace(Input.UrlOrHost) ? null : Input.UrlOrHost.Trim();
        item.IsFavorite = Input.IsFavorite;
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_updated", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("Index");
    }

    public sealed class InputModel
    {
        public Guid Id { get; set; }
        [Required, StringLength(160)] public string Title { get; set; } = string.Empty;
        [Required, EnumDataType(typeof(VaultItemKind))] public VaultItemKind Kind { get; set; }
        [StringLength(2048)] public string? UrlOrHost { get; set; }
        [StringLength(500)] public string? TagsInput { get; set; }
        public bool IsFavorite { get; set; }
        public List<SecretInputModel> Secrets { get; set; } = [];
    }

    public sealed class SecretInputModel
    {
        public Guid Id { get; set; }
        [Required, StringLength(80)] public string FieldName { get; set; } = string.Empty;
        [StringLength(16_000)] public string? Value { get; set; }
        public bool IsNew => Id == Guid.Empty;
        public bool IsLongText => FieldName.Contains("备注", StringComparison.OrdinalIgnoreCase) || FieldName.Contains("笔记", StringComparison.OrdinalIgnoreCase);
        public string InputMode => FieldName.Contains("卡号", StringComparison.OrdinalIgnoreCase) || FieldName.Contains("安全码", StringComparison.OrdinalIgnoreCase) || FieldName.Contains("PIN", StringComparison.OrdinalIgnoreCase) || FieldName.Contains("有效期", StringComparison.OrdinalIgnoreCase) ? "numeric" : "text";
    }
}
