using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Settings;

public sealed class IndexModel(UserManager<VaultUser> users, SignInManager<VaultUser> signIn, VaultDbContext db, SecretCipher cipher) : PageModel
{
    public string? Email { get; private set; }
    public DateTime? CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    public bool HasApiKey { get; private set; }
    public bool AiConfigured { get; private set; }
    [TempData] public string? Notice { get; set; }
    [TempData] public string? AiNotice { get; set; }
    [TempData] public string? Error { get; set; }
    [BindProperty] public PasswordInput Input { get; set; } = new();
    [BindProperty] public AiInputModel AiInput { get; set; } = new();

    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return;
        LoadFacts(user);
        await LoadAiAsync(user);
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        KeepModelStatePrefix("Input.");
        LoadFacts(user);
        await LoadAiAsync(user);
        if (!ModelState.IsValid) return Page();

        var result = await users.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            Error = result.Errors.Any(x => x.Code == "PasswordMismatch")
                ? "当前密码不正确。"
                : "新密码需要至少 12 个字符，并包含大写、小写字母、数字和符号。";
            return Page();
        }

        await signIn.RefreshSignInAsync(user);
        Notice = "密码已更新，其他设备的登录会在一分钟内失效。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAiAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        KeepModelStatePrefix("AiInput.");
        LoadFacts(user);

        var settings = await db.ArticleAiSettings.SingleOrDefaultAsync(x => x.OwnerId == user.Id);
        HasApiKey = settings is not null;
        AiConfigured = settings is not null;
        if (settings is null && string.IsNullOrWhiteSpace(AiInput.ApiKey)) ModelState.AddModelError("AiInput.ApiKey", "首次配置必须填写 API Key。");
        try { ArticleExtractionService.ValidateBaseUrl(AiInput.BaseUrl); }
        catch (InvalidOperationException ex) { ModelState.AddModelError("AiInput.BaseUrl", ex.Message); }
        if (!ModelState.IsValid) return Page();

        settings ??= new ArticleAiSettings { OwnerId = user.Id };
        settings.Provider = AiInput.Provider.Trim().ToLowerInvariant();
        settings.BaseUrl = AiInput.BaseUrl.Trim().TrimEnd('/');
        settings.ModelName = AiInput.ModelName.Trim();
        settings.UpdatedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(AiInput.ApiKey))
        {
            var payload = cipher.EncryptValue($"article-ai-key:{user.Id}", AiInput.ApiKey.Trim());
            settings.ApiKeyCiphertext = payload.Ciphertext;
            settings.ApiKeyNonce = payload.Nonce;
            settings.ApiKeyAuthenticationTag = payload.AuthenticationTag;
            settings.WrappedDataKey = payload.WrappedDataKey;
            settings.KeyWrapNonce = payload.KeyWrapNonce;
            settings.KeyWrapAuthenticationTag = payload.KeyWrapAuthenticationTag;
        }
        if (db.Entry(settings).State == EntityState.Detached) db.ArticleAiSettings.Add(settings);
        await db.SaveChangesAsync();
        AiNotice = "萃取模型配置已保存，API Key 已加密。";
        return Redirect("/Settings#ai");
    }

    private void KeepModelStatePrefix(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(x => !x.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
            ModelState.Remove(key);
    }

    private void LoadFacts(VaultUser user)
    {
        Email = user.Email;
        CreatedAtUtc = user.CreatedAtUtc;
        LastLoginAtUtc = user.LastLoginAtUtc;
    }

    private async Task LoadAiAsync(VaultUser user)
    {
        var settings = await db.ArticleAiSettings.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == user.Id);
        if (settings is null) return;
        HasApiKey = true;
        AiConfigured = true;
        AiInput.Provider = settings.Provider;
        AiInput.BaseUrl = settings.BaseUrl;
        AiInput.ModelName = settings.ModelName;
    }

    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "请输入当前密码。"), DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "请输入新密码。"), StringLength(128, MinimumLength = 12, ErrorMessage = "新密码需要 12 至 128 个字符。"), DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "请再次输入新密码。"), Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致。"), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }

    public sealed class AiInputModel
    {
        [Required(ErrorMessage = "请填写提供商。"), MaxLength(40)]
        public string Provider { get; set; } = "deepseek";

        [Required(ErrorMessage = "请填写 API Base URL。"), MaxLength(300)]
        public string BaseUrl { get; set; } = "https://api.deepseek.com";

        [Required(ErrorMessage = "请填写模型名称。"), MaxLength(100)]
        public string ModelName { get; set; } = "deepseek-flash";

        [MaxLength(500)]
        public string? ApiKey { get; set; }
    }
}
