using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers;

public class LegalController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public LegalController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Terms()
    {
        return View();
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<IActionResult> Accept()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            user.TermsAcceptedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            // 刷新登录状态，确保状态立即生效
            await _signInManager.RefreshSignInAsync(user);
        }
        return RedirectToAction("Index", "Home");
    }
}
