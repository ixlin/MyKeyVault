using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/legal")]
[Authorize]
public class LegalController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private const string CURRENT_TERMS_VERSION = "v1";

    public LegalController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var accepted = await _db.TermsAcceptances.AsNoTracking().AnyAsync(x => x.UserId == user.Id && x.Version == CURRENT_TERMS_VERSION);
        return Ok(new { needAccept = !accepted, version = CURRENT_TERMS_VERSION });
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
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
        return Ok(new { ok = true });
    }
}
 
