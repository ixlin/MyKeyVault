using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Areas.Identity.Pages.Account.Manage;

public class DeletePersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    public DeletePersonalDataModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager){_userManager=userManager; _signInManager=signInManager;}
    public bool RequirePassword { get; set; }
    public class InputModel { [Required(ErrorMessage="必填")] [DataType(DataType.Password)] public string Password { get; set; } = string.Empty; }
    [BindProperty] public InputModel Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(){ var u=await _userManager.GetUserAsync(User); if(u==null) return NotFound(); RequirePassword = await _userManager.HasPasswordAsync(u); return Page(); }
    public async Task<IActionResult> OnPostAsync(){ var u=await _userManager.GetUserAsync(User); if(u==null) return NotFound(); RequirePassword = await _userManager.HasPasswordAsync(u); if(RequirePassword){ if(!ModelState.IsValid) return Page(); if(!await _userManager.CheckPasswordAsync(u, Input.Password)){ ModelState.AddModelError(string.Empty, "密码不正确"); return Page(); } }
        var result=await _userManager.DeleteAsync(u); if(!result.Succeeded){ foreach(var e in result.Errors) ModelState.AddModelError(string.Empty,e.Description); return Page(); } await _signInManager.SignOutAsync(); return Redirect("/"); }
}
