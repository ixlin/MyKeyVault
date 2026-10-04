using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Areas.Identity.Pages.Account;

[AllowAnonymous, EnableRateLimiting("password-recovery"), RequestSizeLimit(8192)]
public sealed class ForgotPasswordModel(PasswordResetService recovery) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string? StatusMessage { get; set; }
    public bool EmailReady => recovery.IsConfigured;
    public sealed class InputModel
    {
        [Required(ErrorMessage = "请输入注册邮箱。"), EmailAddress(ErrorMessage = "请输入有效邮箱。"), StringLength(254)]
        public string Email { get; set; } = "";
        [Required(ErrorMessage = "请输入邮件中的验证码。"), RegularExpression("^[0-9]{6}$", ErrorMessage = "请输入 6 位数字验证码。")]
        public string Code { get; set; } = "";
        [Required(ErrorMessage = "请输入新密码。"), StringLength(128, MinimumLength = 12, ErrorMessage = "密码需要 12 至 128 个字符。"), DataType(DataType.Password)]
        public string Password { get; set; } = "";
        [Required(ErrorMessage = "请再次输入新密码。"), StringLength(128), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "两次密码不一致。")]
        public string ConfirmPassword { get; set; } = "";
    }
    public void OnGet() { }
    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        foreach (var key in ModelState.Keys.Where(x => x != "Input.Email").ToArray()) ModelState.Remove(key);
        ClearSecrets();
        if (!ModelState.IsValid) return Page();
        StatusMessage = await recovery.SendAsync(Input.Email, cancellationToken);
        return Page();
    }
    public async Task<IActionResult> OnPostResetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { ClearSecrets(); return Page(); }
        var result = await recovery.ResetAsync(Input.Email, Input.Code, Input.Password, cancellationToken);
        ClearSecrets();
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Code.StartsWith("Password")
                    ? "新密码需要至少 12 个字符，包含大写、小写字母、数字和符号。" : PasswordResetService.InvalidMessage);
            return Page();
        }
        return RedirectToPage("./ResetPasswordConfirmation");
    }
    private void ClearSecrets()
    {
        Input.Password = Input.ConfirmPassword = "";
        // Retain errors, but never redisplay submitted secrets.
        foreach (var key in new[] { "Input.Password", "Input.ConfirmPassword" })
            if (ModelState.TryGetValue(key, out var entry)) entry.RawValue = entry.AttemptedValue = null;
    }
}
