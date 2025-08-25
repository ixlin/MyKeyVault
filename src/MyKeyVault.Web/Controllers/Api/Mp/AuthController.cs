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
        try
        {
            Console.WriteLine($"🔐 [AUTH] Login attempt for: {req?.Identifier}");
            
            if (req == null || string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.Password))
            {
                Console.WriteLine($"🔐 [AUTH] Missing credentials");
                return BadRequest(new { message = "请输入账号和密码", code = "MISSING_CREDENTIALS" });
            }

            ApplicationUser? user = null;
            
            // 按顺序查找用户：邮箱 -> 手机号 -> 用户名
            if (req.Identifier.Contains('@'))
            {
                Console.WriteLine($"🔐 [AUTH] Looking for user by email: {req.Identifier}");
                user = await _userManager.FindByEmailAsync(req.Identifier);
            }
            if (user == null)
            {
                Console.WriteLine($"🔐 [AUTH] Looking for user by phone: {req.Identifier}");
                user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == req.Identifier);
            }
            if (user == null)
            {
                Console.WriteLine($"🔐 [AUTH] Looking for user by username: {req.Identifier}");
                user = await _userManager.FindByNameAsync(req.Identifier);
            }
            
            if (user == null)
            {
                Console.WriteLine($"🔐 [AUTH] User not found: {req.Identifier}");
                return Unauthorized(new { message = "账号不存在", code = "USER_NOT_FOUND" });
            }

            Console.WriteLine($"🔐 [AUTH] Found user: {user.Email}, attempting password verification...");
            var result = await _signInManager.PasswordSignInAsync(user, req.Password, isPersistent: false, lockoutOnFailure: true);
            
            Console.WriteLine($"🔐 [AUTH] SignIn result - Succeeded: {result.Succeeded}, IsLockedOut: {result.IsLockedOut}, IsNotAllowed: {result.IsNotAllowed}, RequiresTwoFactor: {result.RequiresTwoFactor}");
            
            if (result.IsLockedOut)
            {
                Console.WriteLine($"🔐 [AUTH] Account locked: {user.Email}");
                return Unauthorized(new { message = "账号已被锁定，请稍后再试", code = "ACCOUNT_LOCKED" });
            }
                
            if (result.IsNotAllowed)
            {
                Console.WriteLine($"🔐 [AUTH] Account not allowed: {user.Email}");
                return Unauthorized(new { message = "账号未激活或被禁用", code = "ACCOUNT_NOT_ALLOWED" });
            }
                
            if (result.RequiresTwoFactor)
            {
                Console.WriteLine($"🔐 [AUTH] Two factor required: {user.Email}");
                return Unauthorized(new { message = "需要双重验证", code = "TWO_FACTOR_REQUIRED" });
            }
                
            if (!result.Succeeded)
            {
                Console.WriteLine($"🔐 [AUTH] Wrong password for: {user.Email}");
                return Unauthorized(new { message = "密码错误", code = "WRONG_PASSWORD" });
            }

            Console.WriteLine($"🔐 [AUTH] Login successful for: {user.Email}");
            return Ok(new { ok = true, message = "登录成功" });
        }
        catch (Exception ex)
        {
            // 记录详细错误日志
            Console.WriteLine($"🔐 [AUTH] Login error: {ex.Message}");
            Console.WriteLine($"🔐 [AUTH] Stack trace: {ex.StackTrace}");
            return StatusCode(500, new { message = "服务器内部错误，请稍后再试", code = "SERVER_ERROR" });
        }
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

