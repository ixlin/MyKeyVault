using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            // 获取用户的账号和标签统计
            var accountCount = await _context.Accounts
                .Where(a => a.UserId == userId)
                .CountAsync();

            var tagCount = await _context.Tags
                .Where(t => t.UserId == userId)
                .CountAsync();

            // 获取最近修改的5条账号记录
            var recentAccounts = await _context.Accounts
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.UpdatedAt)
                .Take(5)
                .Include(a => a.AccountTags)
                .ThenInclude(at => at.Tag)
                .Select(a => new
                {
                    id = a.AccountId,
                    title = a.Title,
                    lastModified = a.UpdatedAt,
                    updatedAt = a.UpdatedAt, // 添加这个字段以兼容前端
                    tags = a.AccountTags != null ? a.AccountTags.Where(at => at.Tag != null).Select(at => at.Tag!.TagName).ToArray() : new string[0]
                })
                .ToListAsync();

            var result = new
            {
                accountCount,
                tagCount,
                recentAccounts
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "获取仪表盘数据失败", error = ex.Message });
        }
    }
}
