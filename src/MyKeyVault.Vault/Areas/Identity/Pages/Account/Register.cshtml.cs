using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel(UserManager<VaultUser> userManager, SignInManager<VaultUser> signInManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public sealed class InputModel
    {
        [Required(ErrorMessage = "请输入邮箱地址。")] [EmailAddress(ErrorMessage = "请输入有效的邮箱地址。")] public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "请设置密码。")] [DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
        [Required(ErrorMessage = "请再次输入密码。")] [DataType(DataType.Password)] [Compare(nameof(Password), ErrorMessage = "两次输入的密码不一致。")] public string ConfirmPassword { get; set; } = string.Empty;
    }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = new VaultUser { UserName = Input.Email, Email = Input.Email };
        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded) { foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }
        if (signInManager.Options.SignIn.RequireConfirmedAccount) return RedirectToPage("./RegisterConfirmation", new { email = Input.Email, returnUrl = ReturnUrl });
        await signInManager.SignInAsync(user, isPersistent: false);
        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? Url.Content("~/")! : ReturnUrl);
    }
}
