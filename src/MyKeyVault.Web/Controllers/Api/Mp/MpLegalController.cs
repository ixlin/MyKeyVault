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
public class MpLegalController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MpLegalController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private string CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet("policy")]
    [AllowAnonymous]
    public IActionResult GetPolicy()
    {
        return Ok(new
        {
            title = "隐私政策与服务条款",
            content = @"
# 隐私政策与服务条款

## 数据收集与使用
我们仅收集您主动提供的账号信息，用于为您提供密码管理服务。

## 数据安全
您的所有数据都经过加密存储，我们承诺不会将您的数据分享给第三方。

## Cookie使用
我们使用Cookie来维持您的登录状态，确保服务的正常运行。

## 服务条款
使用本服务即表示您同意遵守我们的使用条款。

最后更新：2025年8月"
        });
    }

    public record AcceptTermsReq(string Version);
    [HttpPost("accept")]
    public async Task<IActionResult> AcceptTerms([FromBody] AcceptTermsReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Version)) return BadRequest(new { message = "version required" });
        
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        
        var existing = await _db.TermsAcceptances
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Version == req.Version);
        
        if (existing == null)
        {
            var acceptance = new TermsAcceptance
            {
                UserId = userId,
                Version = req.Version,
                AcceptedAt = DateTime.UtcNow
            };
            _db.TermsAcceptances.Add(acceptance);
            await _db.SaveChangesAsync();
        }
        
        return Ok(new { accepted = true });
    }
}
