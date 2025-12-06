using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyKeyVault.Web.Services;

namespace MyKeyVault.Web.Controllers;

/// <summary>
/// 微信图文获取控制器
/// </summary>
[Authorize]
public class WechatArticleController : Controller
{
    private readonly WechatScraperService _scraperService;
    private readonly ILogger<WechatArticleController> _logger;

    public WechatArticleController(
        WechatScraperService scraperService,
        ILogger<WechatArticleController> logger)
    {
        _scraperService = scraperService;
        _logger = logger;
    }

    private string CurrentUserId => User?.Identity?.IsAuthenticated == true
        ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value
        : string.Empty;

    /// <summary>
    /// 文章列表页面
    /// </summary>
    public async Task<IActionResult> Index(int page = 1)
    {
        var userId = CurrentUserId;
        var pageSize = 20;
        
        var articles = await _scraperService.GetUserArticlesAsync(userId, page, pageSize);
        var totalCount = await _scraperService.GetUserArticleCountAsync(userId);
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.TotalCount = totalCount;
        
        // 检查服务状态
        ViewBag.ServiceAvailable = await _scraperService.IsServiceAvailableAsync();
        
        return View(articles);
    }

    /// <summary>
    /// 创建/提交爬取任务页面
    /// </summary>
    public async Task<IActionResult> Create()
    {
        ViewBag.ServiceAvailable = await _scraperService.IsServiceAvailableAsync();
        return View();
    }

    /// <summary>
    /// 提交爬取任务
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
        {
            TempData["Error"] = "请输入至少一个微信链接";
            return RedirectToAction(nameof(Create));
        }

        var userId = CurrentUserId;
        
        // 解析链接（按行分割）
        var urlList = urls
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct()
            .ToList();

        if (urlList.Count == 0)
        {
            TempData["Error"] = "请输入有效的微信链接";
            return RedirectToAction(nameof(Create));
        }

        if (urlList.Count > 10)
        {
            TempData["Error"] = "每次最多提交 10 个链接";
            return RedirectToAction(nameof(Create));
        }

        var (success, taskId, error) = await _scraperService.SubmitScrapeTaskAsync(userId, urlList);

        if (!success)
        {
            TempData["Error"] = error ?? "提交失败";
            return RedirectToAction(nameof(Create));
        }

        TempData["Success"] = $"已提交 {urlList.Count} 个链接的爬取任务";
        return RedirectToAction(nameof(Task), new { id = taskId });
    }

    /// <summary>
    /// 任务状态页面
    /// </summary>
    public async Task<IActionResult> Task(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return RedirectToAction(nameof(Index));
        }

        var status = await _scraperService.GetTaskStatusAsync(id);
        if (status == null)
        {
            TempData["Error"] = "任务不存在或已过期";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.TaskId = id;
        return View(status);
    }

    /// <summary>
    /// 获取任务状态（AJAX）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTaskStatus(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            return BadRequest(new { error = "任务ID不能为空" });
        }

        var status = await _scraperService.GetTaskStatusAsync(taskId);
        if (status == null)
        {
            return NotFound(new { error = "任务不存在" });
        }

        return Json(status);
    }

    /// <summary>
    /// 预览文章（重定向到静态文件）
    /// </summary>
    public async Task<IActionResult> Preview(long id)
    {
        var userId = CurrentUserId;
        var article = await _scraperService.GetArticleAsync(id, userId);
        
        if (article == null)
        {
            return NotFound();
        }

        var previewUrl = _scraperService.GetPreviewUrl(article);
        var physicalPath = _scraperService.GetArticlePhysicalPath(article);

        if (string.IsNullOrEmpty(previewUrl) || string.IsNullOrEmpty(physicalPath) || !System.IO.File.Exists(physicalPath))
        {
            TempData["Error"] = "文章尚未完成爬取";
            return RedirectToAction(nameof(Index));
        }

        // 优先使用物理文件输出，避免中文文件名导致的重定向 Header 编码问题
        var contentType = "text/html";
        return PhysicalFile(physicalPath, contentType);
    }

    /// <summary>
    /// 下载文章 ZIP 包
    /// </summary>
    public async Task<IActionResult> Download(long id)
    {
        var userId = CurrentUserId;
        
        var (success, zipPath, error) = await _scraperService.CreateDownloadZipAsync(id, userId);
        
        if (!success || string.IsNullOrEmpty(zipPath))
        {
            TempData["Error"] = error ?? "下载失败";
            return RedirectToAction(nameof(Index));
        }

        var article = await _scraperService.GetArticleAsync(id, userId);
        var fileName = $"{article?.ArticleUniqueId ?? "article"}.zip";

        var bytes = await System.IO.File.ReadAllBytesAsync(zipPath);
        
        // 删除临时文件
        try
        {
            System.IO.File.Delete(zipPath);
        }
        catch { }

        return File(bytes, "application/zip", fileName);
    }

    /// <summary>
    /// 删除文章
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var userId = CurrentUserId;
        var success = await _scraperService.DeleteArticleAsync(id, userId);
        
        if (!success)
        {
            TempData["Error"] = "删除失败，文章不存在";
        }
        else
        {
            TempData["Success"] = "文章已删除";
        }
        
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 删除文章（AJAX）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteAjax(long id)
    {
        var userId = CurrentUserId;
        var success = await _scraperService.DeleteArticleAsync(id, userId);
        
        if (!success)
        {
            return BadRequest(new { error = "删除失败" });
        }
        
        return Ok(new { success = true });
    }
}
