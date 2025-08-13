using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Controllers;

[Authorize]
public class VaultAccountsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<VaultAccountsController> _logger;

    public VaultAccountsController(ApplicationDbContext db, ILogger<VaultAccountsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string CurrentUserId => User?.Identity?.IsAuthenticated == true
        ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value
        : string.Empty;

    public async Task<IActionResult> Index(string? q, long? tagId, DateTime? createdFrom, DateTime? createdTo, string? sort, string? dir)
    {
        var userId = CurrentUserId;
        var query = _db.Accounts.AsNoTracking().Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            // 简单搜索：在服务器端仅对明文字段 Title/Url 做 contains；密文字段留给客户端（后续 E2EE）
            query = query.Where(a => (a.Title != null && a.Title.Contains(q)) || (a.Url != null && a.Url.Contains(q)));
        }
        if (tagId.HasValue)
        {
            var tId = tagId.Value;
            query = query.Where(a => _db.AccountTags.Any(at => at.AccountId == a.AccountId && at.TagId == tId));
        }
        // 创建时间范围筛选（本地日期转UTC）
        if (createdFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(createdFrom.Value.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }
        if (createdTo.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(createdTo.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(a => a.CreatedAt < endUtc);
        }

        // 排序：title / created / updated，默认 updated desc
        var sortKey = (sort ?? "updated").ToLowerInvariant();
        var sortDir = (dir ?? "desc").ToLowerInvariant() == "asc" ? "asc" : "desc";
        query = (sortKey, sortDir) switch
        {
            ("title", "asc") => query.OrderBy(a => a.Title),
            ("title", _) => query.OrderByDescending(a => a.Title),
            ("created", "asc") => query.OrderBy(a => a.CreatedAt),
            ("created", _) => query.OrderByDescending(a => a.CreatedAt),
            ("updated", "asc") => query.OrderBy(a => a.UpdatedAt),
            _ => query.OrderByDescending(a => a.UpdatedAt)
        };

        var items = await query.ToListAsync();
        ViewData["q"] = q;
        ViewData["tagId"] = tagId;
        ViewData["createdFrom"] = createdFrom.HasValue ? createdFrom.Value.ToString("yyyy-MM-dd") : null;
        ViewData["createdTo"] = createdTo.HasValue ? createdTo.Value.ToString("yyyy-MM-dd") : null;
        ViewData["sort"] = sortKey;
        ViewData["dir"] = sortDir;

        // 读取当前用户的全部标签列表与关联计数，用于筛选按钮
        var tags = await _db.Tags.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.TagName)
            .ToListAsync();
        var counts = await (
            from at in _db.AccountTags
            join a in _db.Accounts on at.AccountId equals a.AccountId
            where a.UserId == userId
            group at by at.TagId into g
            select new { TagId = g.Key, Cnt = g.Count() }
        ).ToDictionaryAsync(x => x.TagId, x => x.Cnt);
        ViewBag.AllTags = tags;
        ViewBag.TagCounts = counts;
        return View(items);
    }

    public async Task<IActionResult> Details(long? id)
    {
        if (id == null) return NotFound();
        var item = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == CurrentUserId);
        if (item == null) return NotFound();
        return View(item);
    }

    public async Task<IActionResult> Create()
    {
        // 供视图展示标签选择
        var tags = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId).OrderBy(t => t.TagName).ToListAsync();
        ViewBag.AllTags = tags;
        ViewBag.SelectedTagIds = new HashSet<long>();
        return View(new VaultAccount());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,AccountNameEncrypted,PasswordEncrypted,Url,NoteEncrypted")] VaultAccount account, [FromForm] long[]? selectedTagIds)
    {
        _logger.LogInformation("POST Create called");
    // 先设置服务端字段，再做验证
    account.UserId = CurrentUserId;
    // 移除对 UserId 的模型验证（因其由服务器设置，不从表单绑定）
    ModelState.Remove(nameof(VaultAccount.UserId));
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState invalid on Create: {Errors}", string.Join(";", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            var tagsAll = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId).OrderBy(t => t.TagName).ToListAsync();
            ViewBag.AllTags = tagsAll;
            ViewBag.SelectedTagIds = new HashSet<long>((selectedTagIds ?? Array.Empty<long>()));
            return View(account);
        }
        account.CreatedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();
        // 处理标签关联
        var sel = new HashSet<long>(selectedTagIds ?? Array.Empty<long>());
        if (sel.Count > 0)
        {
            // 仅允许当前用户的标签
            var validTags = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId && sel.Contains(t.TagId)).Select(t => t.TagId).ToListAsync();
            foreach (var tid in validTags)
            {
                _db.AccountTags.Add(new AccountTag { AccountId = account.AccountId, TagId = tid });
            }
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(long? id)
    {
        if (id == null) return NotFound();
        var item = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == CurrentUserId);
        if (item == null) return NotFound();
        // 供视图展示标签选择与已选
        var tags = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId).OrderBy(t => t.TagName).ToListAsync();
        var selected = await _db.AccountTags.AsNoTracking().Where(at => at.AccountId == item.AccountId).Select(at => at.TagId).ToListAsync();
        ViewBag.AllTags = tags;
        ViewBag.SelectedTagIds = new HashSet<long>(selected);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind("AccountId,Title,AccountNameEncrypted,PasswordEncrypted,Url,NoteEncrypted")] VaultAccount account, [FromForm] long[]? selectedTagIds)
    {
        _logger.LogInformation("POST Edit called for {Id}", id);
        if (id != account.AccountId) return NotFound();
        var existing = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == CurrentUserId);
        if (existing == null) return NotFound();
    // 先设置服务端字段，再做验证
    account.UserId = CurrentUserId;
    ModelState.Remove(nameof(VaultAccount.UserId));
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState invalid on Edit: {Errors}", string.Join(";", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            var tagsAll = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId).OrderBy(t => t.TagName).ToListAsync();
            ViewBag.AllTags = tagsAll;
            ViewBag.SelectedTagIds = new HashSet<long>((selectedTagIds ?? Array.Empty<long>()));
            return View(account);
        }
        account.CreatedAt = existing.CreatedAt;
        account.UpdatedAt = DateTime.UtcNow;
        _db.Update(account);
        await _db.SaveChangesAsync();
        // 更新标签关联（先取现有，再做差异）
        var currentTagIds = await _db.AccountTags.AsNoTracking().Where(at => at.AccountId == account.AccountId).Select(at => at.TagId).ToListAsync();
        var desired = new HashSet<long>(selectedTagIds ?? Array.Empty<long>());
        // 仅保留属于当前用户的标签
        if (desired.Count > 0)
        {
            var valid = await _db.Tags.AsNoTracking().Where(t => t.UserId == CurrentUserId && desired.Contains(t.TagId)).Select(t => t.TagId).ToListAsync();
            desired = new HashSet<long>(valid);
        }
        // 需要新增的
        var toAdd = desired.Except(currentTagIds).ToList();
        foreach (var tid in toAdd)
        {
            _db.AccountTags.Add(new AccountTag { AccountId = account.AccountId, TagId = tid });
        }
        // 需要删除的
        var toRemove = currentTagIds.Except(desired).ToList();
        if (toRemove.Count > 0)
        {
            var removeRows = await _db.AccountTags.Where(at => at.AccountId == account.AccountId && toRemove.Contains(at.TagId)).ToListAsync();
            _db.AccountTags.RemoveRange(removeRows);
        }
        if (toAdd.Count > 0 || toRemove.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(long? id)
    {
        if (id == null) return NotFound();
        var item = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == CurrentUserId);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var item = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id && a.UserId == CurrentUserId);
        if (item != null)
        {
            _db.Accounts.Remove(item); // 硬删除
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
