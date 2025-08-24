using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace MyKeyVault.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserKeysController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserKeysController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var uk = await _db.UserKeys.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (uk == null) return NotFound();
        return Ok(new
        {
            uk.KdfSalt,
            uk.KdfParams,
            uk.WrappedDEK,
            uk.CryptoVersion
        });
    }

    // Privacy-preserving salt fetch: always 200 with real or fake salt
    [HttpGet("salt")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSalt([FromQuery] string identifier)
    {
        string RandomSalt()
        {
            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        var iterJson = "{\"iterations\":310000}";
        try
        {
            ApplicationUser? user = null;
            if (!string.IsNullOrWhiteSpace(identifier))
            {
                if (identifier.Contains('@'))
                    user = await _userManager.FindByEmailAsync(identifier);
                if (user == null)
                    user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == identifier);
                if (user == null)
                    user = await _userManager.FindByNameAsync(identifier);
            }
            if (user == null)
            {
                await Task.Delay(Random.Shared.Next(10, 40));
                return Ok(new { KdfSalt = RandomSalt(), KdfParams = iterJson });
            }
            var uk = await _db.UserKeys.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (uk == null)
            {
                return Ok(new { KdfSalt = RandomSalt(), KdfParams = iterJson });
            }
            return Ok(new { uk.KdfSalt, uk.KdfParams });
        }
        catch
        {
            return Ok(new { KdfSalt = RandomSalt(), KdfParams = iterJson });
        }
    }

    public class InitRequest
    {
        public string KdfSalt { get; set; } = string.Empty;
        public string KdfParams { get; set; } = string.Empty;
        public string WrappedDEK { get; set; } = string.Empty;
        public string CryptoVersion { get; set; } = "v1";
    }

    [HttpPost("init")]
    public async Task<IActionResult> Init([FromBody] InitRequest req)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var exists = await _db.UserKeys.AsNoTracking().AnyAsync(x => x.UserId == userId);
        if (exists) return Conflict(new { message = "Already initialized" });
        if (string.IsNullOrWhiteSpace(req.KdfSalt) || string.IsNullOrWhiteSpace(req.WrappedDEK))
        {
            return BadRequest(new { message = "Missing required fields" });
        }
        var uk = new UserKeys
        {
            UserId = userId,
            KdfSalt = req.KdfSalt,
            KdfParams = req.KdfParams,
            WrappedDEK = req.WrappedDEK,
            CryptoVersion = string.IsNullOrWhiteSpace(req.CryptoVersion) ? "v1" : req.CryptoVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserKeys.Add(uk);
        await _db.SaveChangesAsync();
        return Created("api/userkeys", new { message = "OK" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var hasKeys = await _db.UserKeys.AsNoTracking().AnyAsync(x => x.UserId == userId);
        // Detect any ciphertext (very light heuristic: starts with {"v":"v1")
        var hasCipher = await _db.Accounts.AsNoTracking().AnyAsync(a => a.UserId == userId && (
            (a.AccountNameEncrypted != null && a.AccountNameEncrypted.StartsWith("{\"v\":\"v1\"")) ||
            (a.PasswordEncrypted != null && a.PasswordEncrypted.StartsWith("{\"v\":\"v1\"")) ||
            (a.NoteEncrypted != null && a.NoteEncrypted.StartsWith("{\"v\":\"v1\""))
        ));
    return Ok(new { hasKeys, hasCipher });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        // Block reset if any ciphertext exists
        var hasCipher = await _db.Accounts.AsNoTracking().AnyAsync(a => a.UserId == userId && (
            (a.AccountNameEncrypted != null && a.AccountNameEncrypted.StartsWith("{\"v\":\"v1\"")) ||
            (a.PasswordEncrypted != null && a.PasswordEncrypted.StartsWith("{\"v\":\"v1\"")) ||
            (a.NoteEncrypted != null && a.NoteEncrypted.StartsWith("{\"v\":\"v1\""))
        ));
        if (hasCipher) return Conflict(new { message = "Has ciphertext; cannot reset to avoid data loss" });
        var key = await _db.UserKeys.FirstOrDefaultAsync(x => x.UserId == userId);
        if (key != null)
        {
            _db.UserKeys.Remove(key);
            await _db.SaveChangesAsync();
        }
        return Ok(new { message = "reset-ok" });
    }
}
