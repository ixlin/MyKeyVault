using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/vault/accounts")]
[Authorize]
public class MpVaultAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public MpVaultAccountsController(ApplicationDbContext db) { _db = db; }

    private string CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    public record CreateReq(string Title, string? Username, string? EncryptedPassword, string? Website, string? Note, long[]? TagIds);
    public record UpdateReq(string Title, string? Username, string? EncryptedPassword, string? Website, string? Note, long[]? TagIds);
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] long? tagId)
    {
        var userId = CurrentUserId;
        var query = _db.Accounts.AsNoTracking().Where(a => a.UserId == userId);
        if (tagId.HasValue)
        {
            var tid = tagId.Value;
            query = query.Where(a => _db.AccountTags.Any(at => at.AccountId == a.AccountId && at.TagId == tid));
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(a => (a.Title != null && a.Title.Contains(q)) || (a.Url != null && a.Url.Contains(q)));
        }
        var items = await query
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => new
            {
                id = a.AccountId,
                title = a.Title,
                username = a.AccountNameEncrypted,
                website = a.Url,
                updatedAt = a.UpdatedAt,
                tags = _db.AccountTags.Where(at => at.AccountId == a.AccountId)
                    .Join(_db.Tags, at => at.TagId, t => t.TagId, (at, t) => t.TagName).ToList()
            })
            .ToListAsync();
        return Ok(new { items });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get([FromRoute] long id)
    {
        var userId = CurrentUserId;
        var account = await _db.Accounts.AsNoTracking()
            .Where(a => a.AccountId == id && a.UserId == userId)
            .Select(a => new
            {
                id = a.AccountId,
                title = a.Title,
                username = a.AccountNameEncrypted,
                password = a.PasswordEncrypted,
                website = a.Url,
                note = a.NoteEncrypted,
                createdAt = a.CreatedAt,
                updatedAt = a.UpdatedAt,
                tags = _db.AccountTags.Where(at => at.AccountId == a.AccountId)
                    .Join(_db.Tags, at => at.TagId, t => t.TagId, (at, t) => new { id = t.TagId, name = t.TagName }).ToList()
            })
            .FirstOrDefaultAsync();
        if (account == null) return NotFound();
        return Ok(account);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest(new { message = "title required" });
        var userId = CurrentUserId;
        var account = new VaultAccount
        {
            UserId = userId,
            Title = req.Title.Trim(),
            AccountNameEncrypted = req.Username ?? string.Empty,
            PasswordEncrypted = req.EncryptedPassword ?? string.Empty,
            Url = req.Website?.Trim(),
            NoteEncrypted = req.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        // 处理标签关联
        if (req.TagIds?.Any() == true)
        {
            var validTagIds = await _db.Tags.Where(t => t.UserId == userId && req.TagIds.Contains(t.TagId))
                .Select(t => t.TagId).ToListAsync();
            var accountTags = validTagIds.Select(tagId => new AccountTag { AccountId = account.AccountId, TagId = tagId });
            _db.AccountTags.AddRange(accountTags);
            await _db.SaveChangesAsync();
        }
        return Ok(new { id = account.AccountId });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateReq req)
    {
        var userId = CurrentUserId;
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == userId);
        if (account == null) return NotFound();

        account.Title = req.Title.Trim();
        account.AccountNameEncrypted = req.Username ?? string.Empty;
        account.PasswordEncrypted = req.EncryptedPassword ?? string.Empty;
        account.Url = req.Website?.Trim();
        account.NoteEncrypted = req.Note;
        account.UpdatedAt = DateTime.UtcNow;

        // 更新标签关联
        var existingTags = await _db.AccountTags.Where(at => at.AccountId == id).ToListAsync();
        _db.AccountTags.RemoveRange(existingTags);

        if (req.TagIds?.Any() == true)
        {
            var validTagIds = await _db.Tags.Where(t => t.UserId == userId && req.TagIds.Contains(t.TagId))
                .Select(t => t.TagId).ToListAsync();
            var newAccountTags = validTagIds.Select(tagId => new AccountTag { AccountId = id, TagId = tagId });
            _db.AccountTags.AddRange(newAccountTags);
        }

        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var userId = CurrentUserId;
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == userId);
        if (account == null) return NotFound();

        // 删除相关标签
        var accountTags = await _db.AccountTags.Where(at => at.AccountId == id).ToListAsync();
        _db.AccountTags.RemoveRange(accountTags);

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] object req)
    {
        // 简单的搜索功能，基于已有的Get参数实现
        return await List(null, null);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var userId = CurrentUserId;
        var total = await _db.Accounts.CountAsync(a => a.UserId == userId);
        var recentCount = await _db.Accounts
            .Where(a => a.UserId == userId && a.CreatedAt >= DateTime.UtcNow.AddDays(-7))
            .CountAsync();
        return Ok(new { total, recent = recentCount });
    }
}
