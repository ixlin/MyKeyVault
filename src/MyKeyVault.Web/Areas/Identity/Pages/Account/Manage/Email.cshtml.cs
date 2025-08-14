using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Areas.Identity.Pages.Account.Manage;

public class EmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    public EmailModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager){_userManager=userManager; _signInManager=signInManager;}
    [TempData] public string? StatusMessage { get; set; }
    public class InputModel
    {
        [EmailAddress(ErrorMessage="邮箱格式不正确")] [Display(Name="邮箱")] public string? Email { get; set; }
        [Phone(ErrorMessage="手机号格式不正确")] [Display(Name="手机号")] public string? PhoneNumber { get; set; }
    }
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(){ var u=await _userManager.GetUserAsync(User); if(u==null) return NotFound(); Input.Email=u.Email; Input.PhoneNumber=u.PhoneNumber; return Page(); }
    public async Task<IActionResult> OnPostAsync(){ var u=await _userManager.GetUserAsync(User); if(u==null) return NotFound(); if(!ModelState.IsValid) return Page(); if(Input.Email!=u.Email){u.Email=Input.Email; u.UserName=Input.Email??u.UserName;} u.PhoneNumber=Input.PhoneNumber; await _userManager.UpdateAsync(u); await _signInManager.RefreshSignInAsync(u); StatusMessage="已保存"; return RedirectToPage(); }
}
