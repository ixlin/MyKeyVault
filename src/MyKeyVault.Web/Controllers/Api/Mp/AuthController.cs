using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Models;
using MyKeyVault.Web.Services;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/auth")] 
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly WechatMiniProgramService _wechatMiniProgram;
    private readonly IEmailSender _emailSender;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        WechatMiniProgramService wechatMiniProgram,
        IEmailSender emailSender)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _wechatMiniProgram = wechatMiniProgram;
        _emailSender = emailSender;
    }

    public record LoginRequest(string Identifier, string Password);
    public record WechatLoginRequest(string Code);
    public record WechatBindRequest(string Code, string Identifier, string Password);
    public record BindEmailRequest(string Email);

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

    [HttpPost("wechat/login")]
    [AllowAnonymous]
    public async Task<IActionResult> WechatLogin([FromBody] WechatLoginRequest req, CancellationToken cancellationToken)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "微信授权信息缺失", code = "MISSING_WECHAT_CODE" });

        try
        {
            var openId = await _wechatMiniProgram.GetOpenIdAsync(req.Code, cancellationToken);
            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.WechatOpenId == openId, cancellationToken);
            var isNewUser = false;

            if (user == null)
            {
                // 新微信用户可立即使用；邮箱验证状态仅用于持续提醒，不阻断登录。
                user = new ApplicationUser
                {
                    UserName = $"wx_{Guid.NewGuid():N}",
                    WechatOpenId = openId,
                    CreatedAt = DateTime.UtcNow
                };
                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return StatusCode(500, new { message = "创建微信账号失败，请稍后再试", code = "WECHAT_ACCOUNT_CREATE_FAILED" });
                isNewUser = true;
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return Ok(new
            {
                ok = true,
                isNewUser,
                email = user.Email,
                isEmailConfirmed = user.EmailConfirmed,
                emailReminder = !user.EmailConfirmed
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { message = ex.Message, code = "WECHAT_AUTH_UNAVAILABLE" });
        }
    }

    [HttpPost("wechat/bind-existing")]
    [AllowAnonymous]
    public async Task<IActionResult> BindExistingWechat([FromBody] WechatBindRequest req, CancellationToken cancellationToken)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "请填写账号、密码并完成微信授权", code = "MISSING_BINDING_DATA" });

        var openId = await _wechatMiniProgram.GetOpenIdAsync(req.Code, cancellationToken);
        if (await _userManager.Users.AnyAsync(u => u.WechatOpenId == openId, cancellationToken))
            return Conflict(new { message = "该微信已绑定其他账号", code = "WECHAT_ALREADY_BOUND" });

        var user = req.Identifier.Contains('@')
            ? await _userManager.FindByEmailAsync(req.Identifier)
            : await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == req.Identifier, cancellationToken)
              ?? await _userManager.FindByNameAsync(req.Identifier);
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized(new { message = "账号或密码错误", code = "INVALID_CREDENTIALS" });

        user.WechatOpenId = openId;
        user.UpdatedAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return StatusCode(500, new { message = "绑定失败，请稍后重试", code = "WECHAT_BIND_FAILED" });

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Ok(new { ok = true, email = user.Email, isEmailConfirmed = user.EmailConfirmed, emailReminder = !user.EmailConfirmed });
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
        var user = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
        return Ok(new
        {
            isAuthenticated = true,
            userId,
            userName = name,
            email = user?.Email,
            wechatOpenId = user?.WechatOpenId,
            isEmailConfirmed = user?.EmailConfirmed ?? false,
            emailReminder = !(user?.EmailConfirmed ?? false)
        });
    }

    [HttpPost("email/bind")]
    [Authorize]
    public async Task<IActionResult> BindEmail([FromBody] BindEmailRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(req.Email))
            return BadRequest(new { message = "请输入有效邮箱", code = "INVALID_EMAIL" });

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var normalizedEmail = _userManager.NormalizeEmail(req.Email);
        var existing = await _userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && u.Id != user.Id);
        if (existing != null) return Conflict(new { message = "该邮箱已绑定其他账号", code = "EMAIL_ALREADY_USED" });

        await _userManager.SetEmailAsync(user, req.Email);
        user.EmailConfirmed = false;
        user.UpdatedAt = DateTime.UtcNow;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded) return StatusCode(500, new { message = "邮箱绑定失败，请稍后重试" });

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = Url.Page("/Account/ConfirmEmail", null, new { area = "Identity", userId = user.Id, code }, Request.Scheme);
        await _emailSender.SendEmailAsync(req.Email, "确认你的邮箱", $"请点击 <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>此链接</a> 完成邮箱确认。");
        return Ok(new { ok = true, email = user.Email, isEmailConfirmed = false, emailReminder = true });
    }
}
