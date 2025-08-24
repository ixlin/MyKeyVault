using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/vault/accounts")]
[Authorize]
public class VaultAccountsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public VaultAccountsController(ApplicationDbContext db)
    {
        _db = db;
    }

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
                .Join(_db.Tags.Where(t => t.UserId == userId), at => at.TagId, t => t.TagId, (at, t) => new { id = t.TagId, name = t.TagName })
                .ToList()
        }).ToListAsync();
        return Ok(new { items });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail([FromRoute] long id)
    {
        var userId = CurrentUserId;
        var a = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == id && x.UserId == userId);
        if (a == null) return NotFound();
        // 返回密码字段用于密码管理器显示
        return Ok(new
        {
            id = a.AccountId,
            title = a.Title,
            username = a.AccountNameEncrypted,
            encryptedPassword = a.PasswordEncrypted,
            website = a.Url,
            note = a.NoteEncrypted,
            tags = await _db.AccountTags.AsNoTracking()
                .Where(at => at.AccountId == a.AccountId)
                .Join(_db.Tags.AsNoTracking().Where(t => t.UserId == userId), at => at.TagId, t => t.TagId, (at, t) => new { id = t.TagId, name = t.TagName })
                .ToListAsync(),
            createdAt = a.CreatedAt,
            updatedAt = a.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "title required" });
        var a = new VaultAccount
        {
            UserId = CurrentUserId,
            Title = req.Title.Trim(),
            AccountNameEncrypted = req.Username ?? string.Empty,
            PasswordEncrypted = req.EncryptedPassword ?? string.Empty,
            Url = string.IsNullOrWhiteSpace(req.Website) ? null : req.Website,
            NoteEncrypted = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Accounts.Add(a);
        await _db.SaveChangesAsync();
        // 标签
        var tagIds = new HashSet<long>(req.TagIds ?? Array.Empty<long>());
        if (tagIds.Count > 0)
        {
            var valid = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId && tagIds.Contains(t.TagId)).Select(t => t.TagId).ToListAsync();
            foreach (var tid in valid)
            {
                _db.AccountTags.Add(new AccountTag { AccountId = a.AccountId, TagId = tid });
            }
            await _db.SaveChangesAsync();
        }
        return Ok(new { id = a.AccountId });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateReq req)
    {
        var userId = CurrentUserId;
        var a = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id && x.UserId == userId);
        if (a == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.Title)) a.Title = req.Title.Trim();
        if (req.Username != null) a.AccountNameEncrypted = req.Username;
        if (!string.IsNullOrEmpty(req.EncryptedPassword)) a.PasswordEncrypted = req.EncryptedPassword; // 仅非空才覆盖
        a.Url = string.IsNullOrWhiteSpace(req.Website) ? null : req.Website;
        a.NoteEncrypted = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        // 标签
        var desired = new HashSet<long>(req.TagIds ?? Array.Empty<long>());
        if (desired.Count > 0)
        {
            var valid = await _db.Tags.AsNoTracking().Where(t => t.UserId == userId && desired.Contains(t.TagId)).Select(t => t.TagId).ToListAsync();
            desired = new HashSet<long>(valid);
        }
        var current = await _db.AccountTags.AsNoTracking().Where(at => at.AccountId == id).Select(at => at.TagId).ToListAsync();
        var toAdd = desired.Except(current).ToList();
        var toRemove = current.Except(desired).ToList();
        foreach (var tid in toAdd) _db.AccountTags.Add(new AccountTag { AccountId = id, TagId = tid });
        if (toRemove.Count > 0)
        {
            var rows = await _db.AccountTags.Where(at => at.AccountId == id && toRemove.Contains(at.TagId)).ToListAsync();
            _db.AccountTags.RemoveRange(rows);
        }
        if (toAdd.Count > 0 || toRemove.Count > 0) await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id)
    {
        var userId = CurrentUserId;
        var a = await _db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id && x.UserId == userId);
        if (a == null) return NotFound();
        _db.Accounts.Remove(a);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}

