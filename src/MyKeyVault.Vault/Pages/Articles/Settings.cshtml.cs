using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Articles;

public sealed class SettingsModel(VaultDbContext db, UserManager<VaultUser> users, SecretCipher cipher) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public bool HasApiKey { get; private set; }
    [TempData] public string? Notice { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await db.ArticleAiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == users.GetUserId(User)!, cancellationToken);
        if (settings is null) return;
        Input.Provider = settings.Provider;
        Input.BaseUrl = settings.BaseUrl;
        Input.ModelName = settings.ModelName;
        HasApiKey = true;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        var settings = await db.ArticleAiSettings.SingleOrDefaultAsync(x => x.OwnerId == ownerId, cancellationToken);
        HasApiKey = settings is not null;
        if (settings is null && string.IsNullOrWhiteSpace(Input.ApiKey)) ModelState.AddModelError("Input.ApiKey", "首次配置必须填写 API Key。");
        if (!Uri.TryCreate(Input.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback)
            ModelState.AddModelError("Input.BaseUrl", "请输入非本机的 HTTPS API 地址。");
        if (!ModelState.IsValid) return Page();

        settings ??= new ArticleAiSettings { OwnerId = ownerId };
        settings.Provider = Input.Provider.Trim().ToLowerInvariant();
        settings.BaseUrl = Input.BaseUrl.Trim().TrimEnd('/');
        settings.ModelName = Input.ModelName.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(Input.ApiKey))
        {
            var payload = cipher.EncryptValue($"article-ai-key:{ownerId}", Input.ApiKey.Trim());
            settings.ApiKeyCiphertext = payload.Ciphertext;
            settings.ApiKeyNonce = payload.Nonce;
            settings.ApiKeyAuthenticationTag = payload.AuthenticationTag;
            settings.WrappedDataKey = payload.WrappedDataKey;
            settings.KeyWrapNonce = payload.KeyWrapNonce;
            settings.KeyWrapAuthenticationTag = payload.KeyWrapAuthenticationTag;
        }
        if (settings.Id == Guid.Empty || db.Entry(settings).State == EntityState.Detached) db.ArticleAiSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        Notice = "萃取模型配置已保存，API Key 已加密。";
        return RedirectToPage();
    }

    public sealed class InputModel
    {
        [Required, MaxLength(40)] public string Provider { get; set; } = "deepseek";
        [Required, MaxLength(300), Display(Name = "API Base URL")] public string BaseUrl { get; set; } = "https://api.deepseek.com";
        [Required, MaxLength(120), Display(Name = "模型名称")] public string ModelName { get; set; } = "deepseek-chat";
        [MaxLength(500), Display(Name = "API Key")] public string? ApiKey { get; set; }
    }
}
