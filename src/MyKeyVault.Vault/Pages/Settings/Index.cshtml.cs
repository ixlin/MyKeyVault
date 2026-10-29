using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Settings;

public sealed class IndexModel(UserManager<VaultUser> users, SignInManager<VaultUser> signIn) : PageModel
{
    public string? Email { get; private set; }
    public DateTime? CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Error { get; set; }
    [BindProperty] public PasswordInput Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        Email = user?.Email;
        CreatedAtUtc = user?.CreatedAtUtc;
        LastLoginAtUtc = user?.LastLoginAtUtc;
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        Email = user.Email;
        CreatedAtUtc = user.CreatedAtUtc;
        LastLoginAtUtc = user.LastLoginAtUtc;

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

    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "请输入当前密码。"), DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "请输入新密码。"), StringLength(128, MinimumLength = 12, ErrorMessage = "新密码需要 12 至 128 个字符。"), DataType(DataType.Password)]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "请再次输入新密码。"), Compare(nameof(NewPassword), ErrorMessage = "两次输入的新密码不一致。"), DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }
}
