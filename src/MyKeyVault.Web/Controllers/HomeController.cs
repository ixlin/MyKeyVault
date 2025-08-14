using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Models;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using Microsoft.AspNetCore.Authorization;

namespace MyKeyVault.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _db;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var accountsQuery = _db.Accounts.AsNoTracking().Where(a => a.UserId == userId);
            ViewBag.TotalAccounts = await accountsQuery.CountAsync();
            ViewBag.TotalTags = await _db.Tags.AsNoTracking().Where(t => t.UserId == userId).CountAsync();
            ViewBag.Recent = await accountsQuery.OrderByDescending(a => a.UpdatedAt).Take(5)
                .Select(a => new { a.AccountId, a.Title, a.Url, a.UpdatedAt }).ToListAsync();
            return View("Dashboard");
        }
        return View("Landing");
    }

    public IActionResult Privacy() => RedirectToAction("Policy", "Legal");

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
