using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/auth")] 
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    public record LoginRequest(string Identifier, string Password);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "identifier/password required" });

        ApplicationUser? user = null;
        if (req.Identifier.Contains('@'))
            user = await _userManager.FindByEmailAsync(req.Identifier);
        if (user == null)
            user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == req.Identifier);
        if (user == null)
            user = await _userManager.FindByNameAsync(req.Identifier);
        if (user == null)
            return Unauthorized(new { message = "invalid credentials" });

        var result = await _signInManager.PasswordSignInAsync(user, req.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { message = "invalid credentials" });

        return Ok(new { ok = true });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return Ok(new { isAuthenticated = false });
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var name = User.Identity?.Name ?? string.Empty;
        return Ok(new { isAuthenticated = true, userId, userName = name });
    }
}

