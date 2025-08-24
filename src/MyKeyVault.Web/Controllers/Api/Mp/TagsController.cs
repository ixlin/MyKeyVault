using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;

namespace MyKeyVault.Web.Controllers.Api.Mp;

[ApiController]
[Route("api/mp/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public TagsController(ApplicationDbContext db) { _db = db; }

    private string CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool counts = false)
    {
        var uid = CurrentUserId;
        var tags = await _db.Tags.AsNoTracking().Where(t => t.UserId == uid)
            .OrderBy(t => t.TagName)
            .Select(t => new { id = t.TagId, name = t.TagName })
            .ToListAsync();

        if (!counts) return Ok(new { items = tags });

        var useCounts = await _db.AccountTags.AsNoTracking()
            .Where(at => _db.Tags.Any(t => t.TagId == at.TagId && t.UserId == uid))
            .GroupBy(at => at.TagId)
            .Select(g => new { tagId = g.Key, count = g.Count() })
            .ToListAsync();

        var dict = useCounts.ToDictionary(x => x.tagId, x => x.count);
        var items = tags.Select(t => new { t.id, t.name, count = dict.TryGetValue(t.id, out var c) ? c : 0 });
        return Ok(new { items });
    }

    public record CreateReq(string Name);
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReq req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "name required" });
        var name = req.Name.Trim();
        var uid = CurrentUserId;
        var exists = await _db.Tags.AsNoTracking().AnyAsync(t => t.UserId == uid && t.TagName.ToLower() == name.ToLower());
        if (exists) return Conflict(new { message = "duplicate" });
        var tag = new MyKeyVault.Web.Models.Tag { UserId = uid, TagName = name, CreatedAt = DateTime.UtcNow };
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return Ok(new { id = tag.TagId, name = tag.TagName });
    }

    public record UpdateReq(string? Name);
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update([FromRoute] long id, [FromBody] UpdateReq req)
    {
        var uid = CurrentUserId;
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagId == id && t.UserId == uid);
        if (tag == null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            var name = req.Name.Trim();
            var dup = await _db.Tags.AsNoTracking().AnyAsync(t => t.UserId == uid && t.TagId != id && t.TagName.ToLower() == name.ToLower());
            if (dup) return Conflict(new { message = "duplicate" });
            tag.TagName = name;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromRoute] long id, [FromQuery] bool force = false)
    {
        var uid = CurrentUserId;
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagId == id && t.UserId == uid);
        if (tag == null) return NotFound();
        var count = await _db.AccountTags.AsNoTracking().CountAsync(at => at.TagId == id);
        if (count > 0 && !force) return Conflict(new { message = "in-use", count });
        // 删除标签及其绑定
        var binds = await _db.AccountTags.Where(at => at.TagId == id).ToListAsync();
        if (binds.Count > 0) _db.AccountTags.RemoveRange(binds);
        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
