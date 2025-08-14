using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Models;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;

namespace MyKeyVault.Web.Controllers;

public class LegalController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private const string CURRENT_TERMS_VERSION = "v1"; // 升级版本时修改

    public LegalController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Terms()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var accepted = await _db.TermsAcceptances.AnyAsync(x => x.UserId == user.Id && x.Version == CURRENT_TERMS_VERSION);
            ViewBag.CurrentVersionAccepted = accepted;
        }
        ViewBag.Version = CURRENT_TERMS_VERSION;
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
            if (user.TermsAcceptedAt == null)
            {
                user.TermsAcceptedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
            var exists = await _db.TermsAcceptances.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Version == CURRENT_TERMS_VERSION);
            if (exists == null)
            {
                _db.TermsAcceptances.Add(new TermsAcceptance { UserId = user.Id, Version = CURRENT_TERMS_VERSION });
                await _db.SaveChangesAsync();
            }
            await _signInManager.RefreshSignInAsync(user);
        }
        return RedirectToAction(nameof(Policy));
    }

    [Authorize]
    public async Task<IActionResult> Policy()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Index", "Home");
        var list = await _db.TermsAcceptances.Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.AcceptedAt).ToListAsync();
        ViewBag.FirstAcceptedAt = user.TermsAcceptedAt;
        ViewBag.Version = CURRENT_TERMS_VERSION;
        return View(list);
    }
}
