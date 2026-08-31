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
        ValidateEncryptedContent();
        if (!ModelState.IsValid) return Page();
        var userId = users.GetUserId(User)!;
        var item = new VaultItem { OwnerId = userId, Title = Input.Title.Trim(), Kind = Input.Kind, UrlOrHost = string.IsNullOrWhiteSpace(Input.UrlOrHost) ? null : Input.UrlOrHost.Trim(), IsFavorite = Input.IsFavorite };
        var tagNames = (Input.TagsInput ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => x.Trim()).Where(x => x.Length is > 0 and <= 40).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingTags = await db.VaultTags.Where(x => x.OwnerId == userId && tagNames.Contains(x.Name)).ToListAsync(cancellationToken);
        foreach (var tagName in tagNames)
        {
            var tag = existingTags.SingleOrDefault(x => string.Equals(x.Name, tagName, StringComparison.OrdinalIgnoreCase));
            if (tag is null) { tag = new VaultTag { OwnerId = userId, Name = tagName }; db.VaultTags.Add(tag); }
            item.Tags.Add(tag);
        }
        foreach (var (fieldName, value) in EncryptedFields())
            item.Secrets.Add(cipher.Encrypt(item.Id, fieldName, value));
        db.VaultItems.Add(item);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = item.Id, Action = "vault_item_created", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("Index");
    }

    private void ValidateEncryptedContent()
    {
        if (string.IsNullOrWhiteSpace(Input.AccountValue) && string.IsNullOrWhiteSpace(Input.SecretValue) && string.IsNullOrWhiteSpace(Input.Notes)
            && string.IsNullOrWhiteSpace(Input.CardholderName) && string.IsNullOrWhiteSpace(Input.Expiry)
            && string.IsNullOrWhiteSpace(Input.SecurityCode) && string.IsNullOrWhiteSpace(Input.Pin))
            ModelState.AddModelError(string.Empty, "请至少填写账号、卡号、密码或备注中的一项。");
        if (Input.Kind is VaultItemKind.BankCard or VaultItemKind.CreditCard && string.IsNullOrWhiteSpace(Input.AccountValue))
            ModelState.AddModelError("Input.AccountValue", "请填写卡号。");
    }

    private IEnumerable<(string FieldName, string Value)> EncryptedFields()
    {
        if (!string.IsNullOrWhiteSpace(Input.AccountValue))
            yield return (Input.Kind is VaultItemKind.BankCard or VaultItemKind.CreditCard ? "卡号" : "账号信息", Input.AccountValue.Trim());
        if (!string.IsNullOrWhiteSpace(Input.SecretValue))
            yield return (Input.Kind == VaultItemKind.ApiKey ? "API Key / Token" : "密码或密钥", Input.SecretValue);
        if (Input.Kind is VaultItemKind.BankCard or VaultItemKind.CreditCard)
        {
            if (!string.IsNullOrWhiteSpace(Input.CardholderName)) yield return ("持卡人", Input.CardholderName.Trim());
            if (!string.IsNullOrWhiteSpace(Input.Expiry)) yield return ("有效期", Input.Expiry.Trim());
            if (!string.IsNullOrWhiteSpace(Input.SecurityCode)) yield return ("安全码 CVV/CVC", Input.SecurityCode.Trim());
            if (!string.IsNullOrWhiteSpace(Input.Pin)) yield return ("PIN / 取款密码", Input.Pin.Trim());
        }
        if (!string.IsNullOrWhiteSpace(Input.Notes))
            yield return (Input.Kind == VaultItemKind.SecureNote ? "私密笔记" : "备注", Input.Notes.Trim());
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "请填写条目名称。"), StringLength(160, ErrorMessage = "条目名称不能超过 160 个字符。")]
        public string Title { get; set; } = string.Empty;
        [Required, EnumDataType(typeof(VaultItemKind), ErrorMessage = "请选择有效的条目类型。")] public VaultItemKind Kind { get; set; } = VaultItemKind.Login;
        [StringLength(2048, ErrorMessage = "目标地址不能超过 2048 个字符。")] public string? UrlOrHost { get; set; }
        [StringLength(500, ErrorMessage = "标签内容不能超过 500 个字符。")]
        public string? TagsInput { get; set; }
        [StringLength(4_000, ErrorMessage = "账号或卡号不能超过 4000 个字符。")]
        public string? AccountValue { get; set; }
        [StringLength(16_000, ErrorMessage = "密码或密钥不能超过 16000 个字符。")]
        public string? SecretValue { get; set; }
        [StringLength(16_000, ErrorMessage = "备注不能超过 16000 个字符。")]
        public string? Notes { get; set; }
        [StringLength(160, ErrorMessage = "持卡人姓名不能超过 160 个字符。")]
        public string? CardholderName { get; set; }
        [StringLength(32, ErrorMessage = "有效期不能超过 32 个字符。")]
        public string? Expiry { get; set; }
        [StringLength(16, ErrorMessage = "安全码不能超过 16 个字符。")]
        public string? SecurityCode { get; set; }
        [StringLength(64, ErrorMessage = "PIN 不能超过 64 个字符。")]
        public string? Pin { get; set; }
        public bool IsFavorite { get; set; }
    }
}
