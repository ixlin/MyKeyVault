using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel(SignInManager<VaultUser> signInManager, UserManager<VaultUser> userManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public sealed class InputModel
    {
        [Required(ErrorMessage = "请输入邮箱地址。")] [EmailAddress(ErrorMessage = "请输入有效的邮箱地址。")] public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "请输入密码。")] [DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
    public async Task<IActionResult> OnGetAsync() { await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme); return Page(); }
    public async Task<IActionResult> OnPostAsync()
    {
        var returnUrl = string.IsNullOrWhiteSpace(ReturnUrl) ? Url.Content("~/") : ReturnUrl;
        if (!ModelState.IsValid) return Page();
        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await userManager.FindByEmailAsync(Input.Email);
            if (user is not null) { user.LastLoginAtUtc = DateTime.UtcNow; await userManager.UpdateAsync(user); }
            return LocalRedirect(returnUrl!);
        }
        ModelState.AddModelError(string.Empty, result.IsLockedOut ? "该账号已被暂时锁定，请稍后再试。" : result.IsNotAllowed ? "请先完成账号确认后再登录。" : "邮箱或密码不正确。");
        return Page();
    }
}
