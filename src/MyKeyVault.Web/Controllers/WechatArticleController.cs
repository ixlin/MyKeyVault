using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Services;

namespace MyKeyVault.Web.Controllers;

/// <summary>
/// 微信图文获取控制器
/// </summary>
[Authorize]
public class WechatArticleController : Controller
{
    private readonly WechatScraperService _scraperService;
    private readonly AIExtractionService _extractionService;
    private readonly HtmlPresentationService _presentationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<WechatArticleController> _logger;
    private readonly ApplicationDbContext _context;

    public WechatArticleController(
        WechatScraperService scraperService,
        AIExtractionService extractionService,
        HtmlPresentationService presentationService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<WechatArticleController> logger,
        ApplicationDbContext context)
    {
        _scraperService = scraperService;
        _extractionService = extractionService;
        _presentationService = presentationService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _context = context;
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
    /// 获取文章列表（AJAX - 用于自动刷新）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetArticlesAjax(int page = 1)
    {
        var userId = CurrentUserId;
        var pageSize = 20;

        await _scraperService.PumpQueuedScrapeTasksAsync(userId);
        
        var articles = await _scraperService.GetUserArticlesAsync(userId, page, pageSize);
        var totalCount = await _scraperService.GetUserArticleCountAsync(userId);
        
        var result = articles.Select(a => new
        {
            articleId = a.ArticleId,
            taskId = a.TaskId,
            title = a.Title,
            author = a.Author,
            publishTime = a.PublishTime,
            status = a.Status,
            errorMessage = a.ErrorMessage,
            imagesCount = a.ImagesCount,
            videosCount = a.VideosCount,
            sourceUrl = a.SourceUrl,
            createdAt = a.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            previewUrl = _scraperService.GetPreviewUrl(a),
            pdfUrl = _scraperService.GetPdfUrl(a)
        }).ToList();

        return Json(new
        {
            articles = result,
            totalCount,
            currentPage = page,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            hasProcessingTasks = articles.Any(a => a.Status == "processing" || a.Status == "pending" || a.Status == "queued")
        });
    }

    /// <summary>
    /// 任务详情与进度页面
    /// </summary>
    public async Task<IActionResult> Task(string id, long? articleId = null)
    {
        var userId = CurrentUserId;
        string? taskId = id;
        
        // 如果提供了 ArticleId，尝试从数据库查找对应的 TaskId
        if (articleId.HasValue && string.IsNullOrEmpty(taskId))
        {
            var article = await _scraperService.GetArticleAsync(articleId.Value, userId);
            if (article != null && !string.IsNullOrEmpty(article.TaskId))
            {
                taskId = article.TaskId;
            }
        }
        
        if (string.IsNullOrEmpty(taskId))
        {
            TempData["Error"] = "任务ID不存在";
            return RedirectToAction(nameof(TaskManagement));
        }

        var status = await _scraperService.GetTaskStatusAsync(taskId);
        if (status == null)
        {
            TempData["Error"] = "任务不存在或已过期";
            return RedirectToAction(nameof(TaskManagement));
        }

        // 查询数据库中对应的记录，传递给视图
        var dbArticles = await _context.WechatArticles
            .Where(a => a.UserId == userId && a.TaskId == taskId)
            .OrderBy(a => a.ArticleId)
            .ToListAsync();
        
        ViewBag.TaskId = taskId;
        ViewBag.DbArticles = dbArticles;
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

        // 如果 Python 任务已完成或失败，同步更新数据库状态
        if (status.Status == "completed" || status.Status == "failed" || status.Status == "partial")
        {
            var userId = CurrentUserId;
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _scraperService.SyncTaskStatusFromPythonAsync(taskId, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "同步任务状态失败: {TaskId}", taskId);
                }
            });
        }

        return Json(status);
    }

    /// <summary>
    /// 验证URL是否已被爬取（AJAX）
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CheckUrls([FromBody] CheckUrlsRequestDto request)
    {
        if (request?.Urls == null || !request.Urls.Any())
        {
            return BadRequest(new { error = "URL列表不能为空" });
        }

        // 过滤出微信公众号链接
        var wechatUrls = request.Urls
            .Where(url => !string.IsNullOrWhiteSpace(url) && url.Contains("mp.weixin.qq.com"))
            .ToList();

        if (!wechatUrls.Any())
        {
            return Ok(new { results = new Dictionary<string, object>() });
        }

        var userId = CurrentUserId;
        var existingArticles = await _scraperService.CheckUrlsExistAsync(userId, wechatUrls);
        
        var results = existingArticles.Select(kv => new
        {
            url = kv.Key,
            exists = kv.Value != null,
            article = kv.Value != null ? new
            {
                id = kv.Value.ArticleId,
                title = kv.Value.Title,
                status = kv.Value.Status,
                completedAt = (kv.Value.CompletedAt ?? kv.Value.CreatedAt).ToString("yyyy-MM-dd HH:mm"),
                author = kv.Value.Author
            } : null
        }).ToDictionary(x => x.url, x => (object)new { x.exists, x.article });

        return Ok(new { results });
    }

    /// <summary>
    /// 检查URL请求DTO
    /// </summary>
    public class CheckUrlsRequestDto
    {
        public List<string> Urls { get; set; } = new();
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

    #region 任务管理功能

    /// <summary>
    /// 抓取任务管理页面
    /// </summary>
    public async Task<IActionResult> TaskManagement(int page = 1)
    {
        var userId = CurrentUserId;
        var pageSize = 50;

        await _scraperService.PumpQueuedScrapeTasksAsync(userId);
        
        var articles = await _scraperService.GetTaskManagementListAsync(userId, page, pageSize);
        var totalCount = await _scraperService.GetTaskManagementCountAsync(userId);
        
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.TotalCount = totalCount;
        ViewBag.ServiceAvailable = await _scraperService.IsServiceAvailableAsync();
        
        return View(articles);
    }

    /// <summary>
    /// 创建新任务（AJAX - 用于弹窗提交）
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> CreateTaskAjax([FromBody] CreateTaskRequestDto request)
    {
        if (request?.Urls == null || !request.Urls.Any())
        {
            return BadRequest(new { error = "请输入至少一个微信链接" });
        }

        var userId = CurrentUserId;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        // 解析链接
        var urlList = request.Urls
            .SelectMany(u => u.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u) && u.Contains("mp.weixin.qq.com"))
            .Distinct()
            .ToList();

        if (urlList.Count == 0)
        {
            return BadRequest(new { error = "请输入有效的微信链接" });
        }

        if (urlList.Count > 10)
        {
            return BadRequest(new { error = "每次最多提交 10 个链接" });
        }

        var (success, taskId, error) = await _scraperService.SubmitScrapeTaskAsync(userId, urlList);

        // 记录日志
        await _scraperService.WriteLogAsync(
            userId,
            "CreateTask",
            taskId,
            new { urls = urlList, success, error },
            success ? "Success" : "Failed",
            error,
            clientIp);

        if (!success)
        {
            return BadRequest(new { error = error ?? "提交失败" });
        }

        return Ok(new { success = true, taskId, urlCount = urlList.Count });
    }

    /// <summary>
    /// 创建任务请求 DTO
    /// </summary>
    public class CreateTaskRequestDto
    {
        public List<string> Urls { get; set; } = new();
    }

    /// <summary>
    /// 删除任务（带审计日志）- 从任务管理页面调用
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteTaskAjax([FromBody] DeleteTaskRequestDto request)
    {
        if (request == null || !request.ArticleId.HasValue || request.ArticleId.Value <= 0)
        {
            return BadRequest(new { error = "无效的文章ID" });
        }

        var userId = CurrentUserId;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        
        var (success, error, logDetails) = await _scraperService.DeleteArticleWithLogAsync(
            request.ArticleId.Value, 
            userId, 
            clientIp);

        if (!success)
        {
            return BadRequest(new { error = error ?? "删除失败" });
        }

        return Ok(new { success = true, message = "删除成功", logDetails });
    }

    /// <summary>
    /// 删除任务请求 DTO
    /// </summary>
    public class DeleteTaskRequestDto
    {
        public long? ArticleId { get; set; }
        public string? TaskId { get; set; }
    }

    /// <summary>
    /// 重试失败的任务（AJAX）
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RetryTaskAjax([FromBody] RetryTaskRequestDto request)
    {
        if (request == null || !request.ArticleId.HasValue || request.ArticleId.Value <= 0)
        {
            return BadRequest(new { error = "无效的文章ID" });
        }

        var userId = CurrentUserId;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, newTaskId, error) = await _scraperService.RetryArticleAsync(
            request.ArticleId.Value, 
            userId);

        // 记录日志
        await _scraperService.WriteLogAsync(
            userId,
            "RetryTask",
            newTaskId ?? request.ArticleId.ToString(),
            new { articleId = request.ArticleId, newTaskId, success, error },
            success ? "Success" : "Failed",
            error,
            clientIp);

        if (!success)
        {
            return BadRequest(new { error = error ?? "重试失败" });
        }

        return Ok(new { success = true, taskId = newTaskId, message = "已重新提交任务" });
    }

    /// <summary>
    /// 重试任务请求 DTO
    /// </summary>
    public class RetryTaskRequestDto
    {
        public long? ArticleId { get; set; }
    }

    /// <summary>
    /// 获取任务详情（AJAX）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTaskDetail(long id)
    {
        var userId = CurrentUserId;
        var article = await _scraperService.GetArticleAsync(id, userId);
        
        if (article == null)
        {
            return NotFound(new { error = "任务不存在" });
        }

        // 如果有 TaskId 且状态为 processing，尝试获取实时进度
        TaskStatusResponseDto? taskStatus = null;
        if (!string.IsNullOrEmpty(article.TaskId) && article.Status == "processing")
        {
            taskStatus = await _scraperService.GetTaskStatusAsync(article.TaskId);
        }

        return Ok(new
        {
            articleId = article.ArticleId,
            taskId = article.TaskId,
            title = article.Title ?? "处理中...",
            author = article.Author,
            sourceUrl = article.SourceUrl,
            status = article.Status,
            errorMessage = article.ErrorMessage,
            imagesCount = article.ImagesCount,
            videosCount = article.VideosCount,
            createdAt = article.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            completedAt = article.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            // 实时进度（如果有）
            progress = taskStatus?.Articles.FirstOrDefault(a => a.ArticleId == article.ArticleUniqueId)?.Progress ?? 0,
            stage = taskStatus?.Articles.FirstOrDefault(a => a.ArticleId == article.ArticleUniqueId)?.Stage ?? ""
        });
    }
    
    /// <summary>
    /// 从任务详情页删除任务（AJAX）
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteTaskFromDetail([FromBody] DeleteTaskRequestDto request)
    {
        if (string.IsNullOrEmpty(request.TaskId))
        {
            return BadRequest(new { error = "任务ID不能为空" });
        }

        var userId = CurrentUserId;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            // 查找该 TaskId 对应的所有文章
            var articles = await _context.WechatArticles
                .Where(a => a.UserId == userId && a.TaskId == request.TaskId)
                .ToListAsync();

            if (!articles.Any())
            {
                return NotFound(new { error = "任务不存在或已被删除" });
            }

            // 先取消 Python 任务
            await _scraperService.CancelTaskAsync(request.TaskId, deleteFiles: true);

            // 删除所有相关文章
            foreach (var article in articles)
            {
                var (success, error, logDetails) = await _scraperService.DeleteArticleWithLogAsync(
                    article.ArticleId, 
                    userId, 
                    clientIp
                );

                if (!success)
                {
                    _logger.LogWarning("删除文章失败: ArticleId={ArticleId}, Error={Error}", article.ArticleId, error);
                }
            }

            return Ok(new { success = true, message = $"已删除任务及其 {articles.Count} 篇文章" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从详情页删除任务失败: TaskId={TaskId}", request.TaskId);
            return StatusCode(500, new { error = "删除失败：" + ex.Message });
        }
    }

    #endregion

    #region AI 萃取功能

    /// <summary>
    /// AI 配置页面
    /// </summary>
    public async Task<IActionResult> AIConfig()
    {
        var userId = CurrentUserId;
        var config = await _extractionService.GetUserConfigAsync(userId);
        
        return View(config ?? new Models.AIConfig 
        { 
            Provider = "deepseek",
            BaseUrl = "https://api.deepseek.com",
            ModelName = "deepseek-chat"
        });
    }

    /// <summary>
    /// 保存 AI 配置
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AIConfig(Models.AIConfig config)
    {
        var userId = CurrentUserId;
        
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            TempData["Error"] = "API Key 不能为空";
            return View(config);
        }

        var (success, error) = await _extractionService.SaveConfigAsync(userId, config);
        
        if (!success)
        {
            TempData["Error"] = error ?? "保存失败";
            return View(config);
        }

        TempData["Success"] = "AI 配置已保存";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 萃取页面
    /// </summary>
    public async Task<IActionResult> Extract(long id)
    {
        var userId = CurrentUserId;
        var article = await _scraperService.GetArticleAsync(id, userId);
        
        if (article == null)
        {
            TempData["Error"] = "文章不存在";
            return RedirectToAction(nameof(Index));
        }

        if (article.Status != "completed")
        {
            TempData["Error"] = "文章尚未完成下载";
            return RedirectToAction(nameof(Index));
        }

        // 检查是否配置了 AI
        var config = await _extractionService.GetUserConfigAsync(userId);
        if (config == null)
        {
            TempData["Error"] = "请先配置 AI 设置";
            return RedirectToAction(nameof(AIConfig));
        }

        ViewBag.Article = article;
        ViewBag.PreviewUrl = _scraperService.GetPreviewUrl(article);
        
        return View();
    }

    /// <summary>
    /// 执行萃取（AJAX）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DoExtract([FromBody] ExtractRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "提示词不能为空" });
        }

        var userId = CurrentUserId;
        
        var (success, extraction, error) = await _extractionService.ExtractArticleAsync(
            request.ArticleId,
            userId,
            request.Prompt);

        if (!success)
        {
            return BadRequest(new { error = error ?? "萃取失败" });
        }

        return Ok(new
        {
            success = true,
            extractionId = extraction!.ExtractionId,
            result = extraction.Result,
            createdAt = extraction.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    /// <summary>
    /// 获取萃取历史列表（JSON，用于聊天侧边栏）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExtractionHistory(long articleId)
    {
        var userId = CurrentUserId;
        var extractions = await _extractionService.GetArticleExtractionsAsync(articleId, userId);

        var result = extractions.Select(e => new
        {
            extractionId = e.ExtractionId,
            prompt = e.Prompt.Length > 100 ? e.Prompt.Substring(0, 100) + "..." : e.Prompt,
            fullPrompt = e.Prompt,
            result = e.Result,
            status = e.Status,
            modelUsed = e.ModelUsed,
            createdAt = e.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            errorMessage = e.ErrorMessage
        }).ToList();

        return Json(result);
    }

    /// <summary>
    /// 萃取历史列表
    /// </summary>
    public async Task<IActionResult> ExtractionHistory(long id)
    {
        var userId = CurrentUserId;
        var article = await _scraperService.GetArticleAsync(id, userId);
        
        if (article == null)
        {
            TempData["Error"] = "文章不存在";
            return RedirectToAction(nameof(Index));
        }

        var extractions = await _extractionService.GetArticleExtractionsAsync(id, userId);
        
        ViewBag.Article = article;
        return View(extractions);
    }

    /// <summary>
    /// 萃取详情
    /// </summary>
    public async Task<IActionResult> ExtractionDetail(long id)
    {
        var userId = CurrentUserId;
        var extraction = await _extractionService.GetExtractionAsync(id, userId);
        
        if (extraction == null)
        {
            TempData["Error"] = "萃取记录不存在";
            return RedirectToAction(nameof(Index));
        }

        return View(extraction);
    }

    /// <summary>
    /// 萃取请求 DTO
    /// </summary>
    public class ExtractRequestDto
    {
        public long ArticleId { get; set; }
        public string Prompt { get; set; } = string.Empty;
    }

    #endregion

    #region HTML 演示文稿生成功能

    /// <summary>
    /// 生成 HTML 演示文稿（异步启动）
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GeneratePresentation(long extractionId)
    {
        var userId = CurrentUserId;
        
        var extraction = await _extractionService.GetExtractionAsync(extractionId, userId);
        if (extraction == null)
        {
            return BadRequest(new { error = "萃取记录不存在" });
        }

        if (extraction.Status != "completed")
        {
            return BadRequest(new { error = "萃取尚未完成" });
        }

        // 生成唯一进度 Key
        var progressKey = $"html_presentation_{userId}_{extractionId}_{Guid.NewGuid():N}";

        // 后台异步生成（创建新的 Scope 避免 DbContext 被提前释放）
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                // 创建新的服务范围
                using var scope = _serviceScopeFactory.CreateScope();
                var presentationService = scope.ServiceProvider.GetRequiredService<HtmlPresentationService>();
                
                await presentationService.GenerateAsync(extractionId, userId, progressKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成 HTML 演示文稿失败: ExtractionId={ExtractionId}", extractionId);
            }
        });

        return Ok(new { success = true, progressKey });
    }

    /// <summary>
    /// 查询演示文稿生成进度（轮询）
    /// </summary>
    [HttpGet]
    public IActionResult GetPresentationProgress(string progressKey)
    {
        if (string.IsNullOrEmpty(progressKey))
        {
            return BadRequest(new { error = "progressKey 不能为空" });
        }

        var progress = _presentationService.GetProgress(progressKey);
        if (progress == null)
        {
            return NotFound(new { error = "进度信息不存在或已过期" });
        }

        return Ok(progress);
    }

    #endregion

    #region 批量重新抓取

    /// <summary>
    /// 一键批量重新抓取：查出所有 SourceUrl → 去重 → 入队后按限流逐批提交给爬虫
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ReScrapeAll()
    {
        var userId = CurrentUserId;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, articleIds, totalUrls, error) = await _scraperService.QueueReScrapeAllAsync(userId);

        await _scraperService.WriteLogAsync(
            userId,
            "ReScrapeAll",
            null,
            new { totalUrls, queuedArticleIds = articleIds },
            success ? "Success" : "Failed",
            error,
            clientIp);

        if (!success)
        {
            return BadRequest(new { error = error ?? "批量抓取入队失败" });
        }

        return Ok(new
        {
            success = true,
            totalUrls,
            queuedCount = articleIds.Count,
            articleIds
        });
    }

    /// <summary>
    /// 查询批量重新抓取的进度
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ReScrapeProgress([FromQuery] string articleIds)
    {
        if (string.IsNullOrEmpty(articleIds))
        {
            return BadRequest(new { error = "articleIds 不能为空" });
        }

        var ids = articleIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        if (ids.Count == 0)
        {
            return BadRequest(new { error = "articleIds 格式错误" });
        }

        var userId = CurrentUserId;
        await _scraperService.PumpQueuedScrapeTasksAsync(userId);

        var articles = await _context.WechatArticles
            .Where(a => a.UserId == userId && ids.Contains(a.ArticleId))
            .Select(a => new { a.ArticleId, a.Status })
            .ToListAsync();

        int completed = 0, processing = 0, pending = 0, queued = 0, failed = 0, cancelled = 0;

        foreach (var article in articles)
        {
            switch (article.Status)
            {
                case "completed": completed++; break;
                case "processing": processing++; break;
                case "pending": pending++; break;
                case "queued": queued++; break;
                case "failed": failed++; break;
                case "cancelled": cancelled++; break;
                default: pending++; break;
            }
        }

        var missing = ids.Count(id => articles.All(a => a.ArticleId != id));
        failed += missing;
        var allFinished = (processing + pending + queued) == 0;

        return Ok(new
        {
            total = articles.Count + missing,
            completed,
            processing,
            pending,
            queued,
            failed,
            cancelled,
            allFinished
        });
    }

    #endregion
}
