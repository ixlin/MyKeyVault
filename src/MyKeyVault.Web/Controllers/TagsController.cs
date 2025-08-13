using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers;

[Authorize]
public class TagsController : Controller
{
    private readonly ApplicationDbContext _db;
    public TagsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string CurrentUserId => User?.Identity?.IsAuthenticated == true
        ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value
        : string.Empty;

    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;
        var tags = await _db.Tags.AsNoTracking().Where(t => t.UserId == userId)
            .OrderBy(t => t.TagName).ToListAsync();
        // 统计每个标签关联账号数（仅统计当前用户的账号）
        var tagIds = tags.Select(t => t.TagId).ToList();
        var counts = await _db.AccountTags
            .Where(at => tagIds.Contains(at.TagId) && _db.Accounts.Any(a => a.AccountId == at.AccountId && a.UserId == userId))
            .GroupBy(at => at.TagId)
            .Select(g => new { TagId = g.Key, Cnt = g.Count() })
            .ToDictionaryAsync(x => x.TagId, x => x.Cnt);
        ViewBag.TagCounts = counts; // Dictionary<long,int>
        return View(tags);
    }

    public IActionResult Create() => View(new Tag());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("TagName")] Tag tag)
    {
    // 先设置服务端字段，再做验证
    tag.UserId = CurrentUserId;
    ModelState.Remove(nameof(Tag.UserId));
        // 标准化标签名（去空白）
        tag.TagName = tag.TagName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tag.TagName))
        {
            ModelState.AddModelError(nameof(Tag.TagName), "标签名不能为空");
        }
        // 同名预检查，避免触发唯一索引异常
        var exists = await _db.Tags.AsNoTracking().AnyAsync(t => t.UserId == tag.UserId && t.TagName == tag.TagName);
        if (exists)
        {
            ModelState.AddModelError(nameof(Tag.TagName), "该标签已存在");
        }
    if (!ModelState.IsValid) return View(tag);
        tag.CreatedAt = DateTime.UtcNow;
        _db.Tags.Add(tag);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 并发/竞态下的兜底处理
            ModelState.AddModelError(nameof(Tag.TagName), "该标签已存在");
            return View(tag);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(long? id)
    {
        if (id == null) return NotFound();
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagId == id && t.UserId == CurrentUserId);
        if (tag == null) return NotFound();
        return View(tag);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind("TagId,TagName")] Tag tag)
    {
        if (id != tag.TagId) return NotFound();
        var existing = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.TagId == id && t.UserId == CurrentUserId);
        if (existing == null) return NotFound();
    // 先设置服务端字段，再做验证
    tag.UserId = CurrentUserId;
    ModelState.Remove(nameof(Tag.UserId));
        // 标准化
        tag.TagName = tag.TagName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tag.TagName))
        {
            ModelState.AddModelError(nameof(Tag.TagName), "标签名不能为空");
        }
        // 同名预检查（排除自身）
        var dup = await _db.Tags.AsNoTracking().AnyAsync(t => t.UserId == tag.UserId && t.TagId != tag.TagId && t.TagName == tag.TagName);
        if (dup)
        {
            ModelState.AddModelError(nameof(Tag.TagName), "该标签已存在");
        }
    if (!ModelState.IsValid) return View(tag);
        tag.CreatedAt = existing.CreatedAt;
        _db.Update(tag);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Tag.TagName), "该标签已存在");
            return View(tag);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(long? id)
    {
        if (id == null) return NotFound();
        var tag = await _db.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.TagId == id && t.UserId == CurrentUserId);
        if (tag == null) return NotFound();
        return View(tag);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.TagId == id && t.UserId == CurrentUserId);
        if (tag != null)
        {
            _db.Tags.Remove(tag);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
