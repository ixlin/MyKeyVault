using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Areas.Identity.Pages.Account.Manage;

public class ChangePasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<ChangePasswordModel> _logger;
    public ChangePasswordModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ILogger<ChangePasswordModel> logger)
    { _userManager = userManager; _signInManager = signInManager; _logger = logger; }

    [TempData] public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "必填")] [DataType(DataType.Password)] [Display(Name="当前密码")] public string OldPassword { get; set; } = string.Empty;
        [Required(ErrorMessage = "必填")] [StringLength(100, MinimumLength = 12, ErrorMessage = "长度 12-100")] [DataType(DataType.Password)] [Display(Name="新密码")] public string NewPassword { get; set; } = string.Empty;
        [DataType(DataType.Password)] [Display(Name="确认新密码")] [Compare("NewPassword", ErrorMessage = "两次输入不一致")] public string ConfirmPassword { get; set; } = string.Empty;
    }
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        var result = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }
        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "密码已更新";
        return RedirectToPage();
    }
}
