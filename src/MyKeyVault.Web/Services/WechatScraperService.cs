using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Services;

/// <summary>
/// 微信爬虫服务配置
/// </summary>
public class WechatScraperOptions
{
    /// <summary>
    /// Python 爬虫服务地址
    /// </summary>
    public string ServiceUrl { get; set; } = "http://127.0.0.1:5001";
    
    /// <summary>
    /// 文章输出基础目录（相对于 wwwroot）
    /// </summary>
    public string OutputPath { get; set; } = "wechat-articles";
    
    /// <summary>
    /// 每次最多爬取数量
    /// </summary>
    public int MaxArticlesPerRequest { get; set; } = 10;

    /// <summary>
    /// 批量重抓每个 Python 任务包含的文章数
    /// </summary>
    public int BatchArticlesPerTask { get; set; } = 3;

    /// <summary>
    /// 单用户同时运行的 Python 抓取任务数
    /// </summary>
    public int MaxActiveTasksPerUser { get; set; } = 1;

    /// <summary>
    /// queued/pending 状态超过多少分钟判定为过期
    /// </summary>
    public int StalePendingMinutes { get; set; } = 30;

    /// <summary>
    /// processing 状态超过多少分钟判定为过期
    /// </summary>
    public int StaleProcessingMinutes { get; set; } = 20;
}

/// <summary>
/// 爬取请求 DTO
/// </summary>
public class ScrapeRequestDto
{
    [JsonPropertyName("urls")]
    public List<string> Urls { get; set; } = new();
    
    [JsonPropertyName("output_base")]
    public string OutputBase { get; set; } = string.Empty;
    
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// 爬取响应 DTO
/// </summary>
public class ScrapeResponseDto
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("articles")]
    public List<ArticleResultDto> Articles { get; set; } = new();
    
    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; set; }
    
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}

/// <summary>
/// 任务状态响应 DTO
/// </summary>
public class TaskStatusResponseDto
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("articles")]
    public List<ArticleResultDto> Articles { get; set; } = new();
    
    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; set; }
    
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
    
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = string.Empty;
}

/// <summary>
/// 文章结果 DTO
/// </summary>
public class ArticleResultDto
{
    [JsonPropertyName("article_id")]
    public string ArticleId { get; set; } = string.Empty;
    
    [JsonPropertyName("source_url")]
    public string SourceUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string? Title { get; set; }
    
    [JsonPropertyName("author")]
    public string? Author { get; set; }
    
    [JsonPropertyName("publish_time")]
    public string? PublishTime { get; set; }
    
    [JsonPropertyName("html_file_path")]
    public string? HtmlFilePath { get; set; }
    
    [JsonPropertyName("pdf_file_path")]
    public string? PdfFilePath { get; set; }
    
    [JsonPropertyName("images_count")]
    public int ImagesCount { get; set; }
    
    [JsonPropertyName("videos_count")]
    public int VideosCount { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 取消任务响应 DTO
/// </summary>
public class CancelTaskResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("stopped_articles")]
    public List<string> StoppedArticles { get; set; } = new();
    
    [JsonPropertyName("deleted_dirs")]
    public List<string> DeletedDirs { get; set; } = new();
}

/// <summary>
/// 微信爬虫服务
/// </summary>
public class WechatScraperService
{
    private const string StatusQueued = "queued";
    private const string StatusPending = "pending";
    private const string StatusProcessing = "processing";
    private const string StatusCompleted = "completed";
    private const string StatusFailed = "failed";
    private const string StatusCancelled = "cancelled";

    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly WechatScraperOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<WechatScraperService> _logger;

    public WechatScraperService(
        HttpClient httpClient,
        ApplicationDbContext context,
        IOptions<WechatScraperOptions> options,
        IWebHostEnvironment env,
        ILogger<WechatScraperService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _options = options.Value;
        _env = env;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri(_options.ServiceUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // 爬取可能需要较长时间
    }

    /// <summary>
    /// 检查 Python 服务是否可用
    /// </summary>
    public async Task<bool> IsServiceAvailableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python 爬虫服务不可用");
            return false;
        }
    }

    /// <summary>
    /// 检查URL是否已被爬取（仅限当前用户）
    /// </summary>
    public async Task<Dictionary<string, WechatArticle?>> CheckUrlsExistAsync(string userId, List<string> urls)
    {
        var result = new Dictionary<string, WechatArticle?>();

        foreach (var originalUrl in urls)
        {
            if (!TryParseWechatArticleKey(originalUrl, out var key))
            {
                result[originalUrl] = null;
                continue;
            }

            // 先用 SQL 做“缩小范围”的筛选，再在内存里做精确解析比对，避免前缀误判。
            var query = _context.WechatArticles
                .AsNoTracking()
                .Where(a => a.UserId == userId);

            query = key.Type switch
            {
                WechatArticleKeyType.SPathToken => query
                    .Where(a => a.SourceUrl.Contains("mp.weixin.qq.com") && a.SourceUrl.Contains($"/s/{key.Token}")),

                WechatArticleKeyType.Sn => query
                    .Where(a => a.SourceUrl.Contains("mp.weixin.qq.com") && a.SourceUrl.Contains("/s?") && a.SourceUrl.Contains($"sn={key.Token}")),

                WechatArticleKeyType.BizMidIdx => query
                    .Where(a => a.SourceUrl.Contains("mp.weixin.qq.com")
                                && a.SourceUrl.Contains($"__biz={key.Biz}")
                                && a.SourceUrl.Contains($"mid={key.Mid}")
                                && a.SourceUrl.Contains($"idx={key.Idx}")),

                _ => query
            };

            // 候选集不需要太大；最终精确匹配靠解析。
            var candidates = await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .ToListAsync();

            WechatArticle? matched = null;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate.SourceUrl))
                {
                    continue;
                }

                if (!TryParseWechatArticleKey(candidate.SourceUrl, out var candidateKey))
                {
                    continue;
                }

                if (candidateKey.Equals(key))
                {
                    matched = candidate;
                    break;
                }
            }

            result[originalUrl] = matched;
        }

        return result;
    }

    private enum WechatArticleKeyType
    {
        SPathToken,
        Sn,
        BizMidIdx
    }

    private readonly record struct WechatArticleKey(WechatArticleKeyType Type, string Token, string? Biz, string? Mid, string? Idx);

    /// <summary>
    /// 解析微信文章链接，提取“稳定标识”，用于精确判重。
    /// 支持：
    /// - https://mp.weixin.qq.com/s/xxxxxx  -> Token=xxxxxx
    /// - https://mp.weixin.qq.com/s?sn=xxx  -> Token=sn
    /// - https://mp.weixin.qq.com/s?__biz=...&mid=...&idx=... -> Biz/Mid/Idx
    /// </summary>
    private static bool TryParseWechatArticleKey(string url, out WechatArticleKey key)
    {
        key = default;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host) || !uri.Host.Contains("mp.weixin.qq.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = uri.AbsolutePath ?? string.Empty;

        if (path.StartsWith("/s/", StringComparison.OrdinalIgnoreCase))
        {
            var token = path.Substring(3);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            // 防止多余的“/”或空白
            token = token.Trim('/').Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            key = new WechatArticleKey(WechatArticleKeyType.SPathToken, token, null, null, null);
            return true;
        }

        if (path.Equals("/s", StringComparison.OrdinalIgnoreCase))
        {
            var query = QueryHelpers.ParseQuery(uri.Query ?? string.Empty);

            if (query.TryGetValue("sn", out var sn) && !string.IsNullOrWhiteSpace(sn.ToString()))
            {
                key = new WechatArticleKey(WechatArticleKeyType.Sn, sn.ToString(), null, null, null);
                return true;
            }

            if (query.TryGetValue("__biz", out var biz)
                && query.TryGetValue("mid", out var mid)
                && query.TryGetValue("idx", out var idx)
                && !string.IsNullOrWhiteSpace(biz.ToString())
                && !string.IsNullOrWhiteSpace(mid.ToString())
                && !string.IsNullOrWhiteSpace(idx.ToString()))
            {
                key = new WechatArticleKey(WechatArticleKeyType.BizMidIdx, "", biz.ToString(), mid.ToString(), idx.ToString());
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 提交爬取任务
    /// </summary>
    public async Task<(bool success, string? taskId, string? error)> SubmitScrapeTaskAsync(
        string userId, 
        List<string> urls)
    {
        var (success, result, error) = await SubmitScrapeTaskToPythonAsync(userId, urls);
        if (!success || result == null)
        {
            return (false, null, error);
        }

        // 保存文章记录到数据库
        foreach (var article in result.Articles)
        {
            var dbArticle = new WechatArticle
            {
                UserId = userId,
                SourceUrl = article.SourceUrl,
                ArticleUniqueId = article.ArticleId,
                TaskId = result.TaskId,
                Status = StatusPending,
                CreatedAt = DateTime.UtcNow
            };
            _context.WechatArticles.Add(dbArticle);
        }
        await _context.SaveChangesAsync();

        _logger.LogInformation("爬取任务已提交: TaskId={TaskId}, 文章数={Count}", result.TaskId, result.TotalCount);

        return (true, result.TaskId, null);
    }

    private async Task<(bool success, ScrapeResponseDto? result, string? error)> SubmitScrapeTaskToPythonAsync(
        string userId,
        List<string> urls)
    {
        if (urls.Count > _options.MaxArticlesPerRequest)
        {
            return (false, null, $"每次最多爬取 {_options.MaxArticlesPerRequest} 篇文章");
        }

        foreach (var url in urls)
        {
            if (!url.Contains("mp.weixin.qq.com"))
            {
                return (false, null, $"无效的微信链接: {url}");
            }
        }

        try
        {
            // 构建输出路径
            var outputBase = Path.Combine(_env.WebRootPath, _options.OutputPath);
            
            // 确保目录存在
            Directory.CreateDirectory(outputBase);

            var request = new ScrapeRequestDto
            {
                Urls = urls,
                OutputBase = outputBase,
                UserId = userId
            };

            _logger.LogInformation("提交爬取任务: {UrlCount} 个链接, 用户: {UserId}", urls.Count, userId);

            var response = await _httpClient.PostAsJsonAsync("/api/scrape", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("爬取任务提交失败: {StatusCode}, {Error}", response.StatusCode, errorContent);
                return (false, null, $"服务错误: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<ScrapeResponseDto>();
            if (result == null)
            {
                return (false, null, "解析响应失败");
            }

            return (true, result, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "无法连接到爬虫服务");
            return (false, null, "无法连接到爬虫服务，请稍后重试");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提交爬取任务失败");
            return (false, null, $"提交失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 查询任务状态并更新数据库
    /// </summary>
    public async Task<TaskStatusResponseDto?> GetTaskStatusAsync(string taskId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/task/{taskId}");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("获取任务状态失败: {TaskId}, {StatusCode}", taskId, response.StatusCode);
                
                // 如果 Python 服务返回 404，说明任务已丢失（可能是服务重启）
                // 需要将数据库中仍处于 pending/processing 的文章标记为失败
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await MarkOrphanedArticlesAsFailedAsync(taskId);
                }
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TaskStatusResponseDto>();
            
            if (result != null)
            {
                // 更新数据库中的文章状态
                await UpdateArticleStatusAsync(result);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "无法连接到爬虫服务: {TaskId}", taskId);
            // 连接失败时不立即标记为失败，可能只是临时网络问题
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务状态失败: {TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// 将孤立的文章（Python 服务中找不到对应任务）标记为失败
    /// </summary>
    private async Task MarkOrphanedArticlesAsFailedAsync(string taskId)
    {
        try
        {
            var orphanedArticles = await _context.WechatArticles
                .Where(a => a.TaskId == taskId && (a.Status == "pending" || a.Status == "processing"))
                .ToListAsync();

            if (!orphanedArticles.Any())
            {
                return;
            }

            foreach (var article in orphanedArticles)
            {
                article.Status = "failed";
                article.ErrorMessage = "任务已丢失（爬虫服务可能已重启），请重新提交";
                article.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogWarning("已将 {Count} 个孤立文章标记为失败: TaskId={TaskId}", orphanedArticles.Count, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "标记孤立文章失败: {TaskId}", taskId);
        }
    }

    /// <summary>
    /// 从 Python 服务同步任务状态到数据库
    /// </summary>
    public async Task<bool> SyncTaskStatusFromPythonAsync(string taskId, string userId)
    {
        try
        {
            // 从 Python 获取最新状态
            var taskStatus = await GetTaskStatusAsync(taskId);
            if (taskStatus == null)
            {
                _logger.LogWarning("同步任务状态失败：Python 服务中未找到任务 {TaskId}", taskId);
                return false;
            }

            // 更新数据库
            await UpdateArticleStatusAsync(taskStatus);
            
            _logger.LogInformation("成功同步任务状态: {TaskId}, Status: {Status}", taskId, taskStatus.Status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步任务状态异常: {TaskId}", taskId);
            return false;
        }
    }

    /// <summary>
    /// 同步当前用户处于 pending/processing 的任务状态。
    /// </summary>
    public async Task<int> SyncActiveTaskStatusesAsync(string userId, int maxTasks = 50)
    {
        var activeTaskIds = await _context.WechatArticles
            .Where(a => a.UserId == userId
                        && !string.IsNullOrEmpty(a.TaskId)
                        && (a.Status == StatusPending || a.Status == StatusProcessing))
            .GroupBy(a => a.TaskId!)
            .OrderBy(g => g.Min(a => a.CreatedAt))
            .Select(g => g.Key)
            .Take(maxTasks)
            .ToListAsync();

        if (!activeTaskIds.Any())
        {
            return 0;
        }

        if (!await IsServiceAvailableAsync())
        {
            return 0;
        }

        var synced = 0;
        foreach (var taskId in activeTaskIds)
        {
            var taskStatus = await GetTaskStatusAsync(taskId);
            if (taskStatus != null)
            {
                synced++;
            }
        }

        return synced;
    }

    public async Task<int> CleanupStaleTasksAsync(string userId)
    {
        var now = DateTime.UtcNow;
        var stalePendingBefore = now.AddMinutes(-Math.Max(1, _options.StalePendingMinutes));
        var staleProcessingBefore = now.AddMinutes(-Math.Max(1, _options.StaleProcessingMinutes));
        var cleaned = 0;

        var staleQueued = await _context.WechatArticles
            .Where(a => a.UserId == userId
                        && a.Status == StatusQueued
                        && a.CreatedAt < stalePendingBefore)
            .ToListAsync();

        foreach (var article in staleQueued)
        {
            article.Status = StatusFailed;
            article.ErrorMessage = "排队任务已超时清理，请重新提交";
            article.CompletedAt = now;
        }

        cleaned += staleQueued.Count;

        var staleTaskIds = await _context.WechatArticles
            .Where(a => a.UserId == userId
                        && !string.IsNullOrEmpty(a.TaskId)
                        && ((a.Status == StatusPending && a.CreatedAt < stalePendingBefore)
                            || (a.Status == StatusProcessing && a.CreatedAt < staleProcessingBefore)))
            .GroupBy(a => a.TaskId!)
            .Select(g => g.Key)
            .ToListAsync();

        if (staleTaskIds.Any() && await IsServiceAvailableAsync())
        {
            foreach (var taskId in staleTaskIds)
            {
                var status = await GetTaskStatusAsync(taskId);
                if (status != null)
                {
                    continue;
                }

                var staleArticles = await _context.WechatArticles
                    .Where(a => a.UserId == userId
                                && a.TaskId == taskId
                                && (a.Status == StatusPending || a.Status == StatusProcessing))
                    .ToListAsync();

                foreach (var article in staleArticles)
                {
                    article.Status = StatusFailed;
                    article.ErrorMessage = "任务状态无法确认且已超时，请重新提交";
                    article.CompletedAt = now;
                }

                cleaned += staleArticles.Count;
            }
        }

        if (cleaned > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogWarning("已清理 {Count} 个过期微信抓取任务记录，用户: {UserId}", cleaned, userId);
        }

        return cleaned;
    }

    public async Task<bool> HasActiveScrapeWorkAsync(string userId)
    {
        await CleanupStaleTasksAsync(userId);

        return await _context.WechatArticles
            .AnyAsync(a => a.UserId == userId
                           && (a.Status == StatusQueued
                               || a.Status == StatusPending
                               || a.Status == StatusProcessing));
    }

    public async Task<(bool success, List<long> articleIds, int totalUrls, string? error)> QueueReScrapeAllAsync(string userId)
    {
        if (await HasActiveScrapeWorkAsync(userId))
        {
            return (false, new(), 0, "当前还有抓取任务正在排队或执行，请等完成后再发起批量抓取");
        }

        var sourceUrls = await _context.WechatArticles
            .Where(a => a.UserId == userId && !string.IsNullOrEmpty(a.SourceUrl))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.SourceUrl)
            .ToListAsync();

        var distinctUrls = GetDistinctWechatUrls(sourceUrls);
        if (distinctUrls.Count == 0)
        {
            return (false, new(), 0, "没有找到可重新抓取的原文链接");
        }

        var now = DateTime.UtcNow;
        var queuedArticles = distinctUrls.Select(url => new WechatArticle
        {
            UserId = userId,
            SourceUrl = url,
            Status = StatusQueued,
            CreatedAt = now
        }).ToList();

        _context.WechatArticles.AddRange(queuedArticles);
        await _context.SaveChangesAsync();

        var started = await PumpQueuedScrapeTasksAsync(userId);
        _logger.LogInformation("批量重抓已入队: UserId={UserId}, Total={Total}, StartedTasks={Started}", userId, queuedArticles.Count, started);

        return (true, queuedArticles.Select(a => a.ArticleId).ToList(), queuedArticles.Count, null);
    }

    public async Task<int> PumpQueuedScrapeTasksAsync(string userId)
    {
        await CleanupStaleTasksAsync(userId);
        await SyncActiveTaskStatusesAsync(userId);

        if (!await IsServiceAvailableAsync())
        {
            return 0;
        }

        var activeTasks = await _context.WechatArticles
            .Where(a => a.UserId == userId
                        && !string.IsNullOrEmpty(a.TaskId)
                        && (a.Status == StatusPending || a.Status == StatusProcessing))
            .GroupBy(a => a.TaskId!)
            .CountAsync();

        var slots = Math.Max(0, _options.MaxActiveTasksPerUser - activeTasks);
        if (slots == 0)
        {
            return 0;
        }

        var started = 0;
        var batchSize = Math.Clamp(_options.BatchArticlesPerTask, 1, _options.MaxArticlesPerRequest);

        for (var i = 0; i < slots; i++)
        {
            var queuedArticles = await _context.WechatArticles
                .Where(a => a.UserId == userId && a.Status == StatusQueued)
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.ArticleId)
                .Take(batchSize)
                .ToListAsync();

            if (!queuedArticles.Any())
            {
                break;
            }

            var urls = queuedArticles.Select(a => a.SourceUrl).ToList();
            var (success, result, error) = await SubmitScrapeTaskToPythonAsync(userId, urls);
            var now = DateTime.UtcNow;

            if (!success || result == null)
            {
                foreach (var article in queuedArticles)
                {
                    article.Status = StatusFailed;
                    article.ErrorMessage = error ?? "提交爬虫服务失败";
                    article.CompletedAt = now;
                }

                await _context.SaveChangesAsync();
                _logger.LogWarning("提交排队抓取批次失败: UserId={UserId}, Count={Count}, Error={Error}", userId, queuedArticles.Count, error);
                continue;
            }

            if (result.Articles.Count != queuedArticles.Count)
            {
                foreach (var article in queuedArticles)
                {
                    article.Status = StatusFailed;
                    article.ErrorMessage = "爬虫服务返回文章数量不一致，请重新提交";
                    article.CompletedAt = now;
                }

                await _context.SaveChangesAsync();
                _logger.LogWarning(
                    "爬虫服务返回文章数量不一致: UserId={UserId}, Expected={Expected}, Actual={Actual}, TaskId={TaskId}",
                    userId,
                    queuedArticles.Count,
                    result.Articles.Count,
                    result.TaskId);
                continue;
            }

            for (var index = 0; index < queuedArticles.Count; index++)
            {
                var article = queuedArticles[index];
                var articleResult = result.Articles.ElementAtOrDefault(index);

                article.TaskId = result.TaskId;
                article.ArticleUniqueId = articleResult?.ArticleId;
                article.Status = StatusPending;
                article.ErrorMessage = null;
                article.CompletedAt = null;
            }

            await _context.SaveChangesAsync();
            started++;
        }

        return started;
    }

    private static List<string> GetDistinctWechatUrls(IEnumerable<string> urls)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.Contains("mp.weixin.qq.com"))
            {
                continue;
            }

            var key = TryParseWechatArticleKey(url, out var articleKey)
                ? $"{articleKey.Type}:{articleKey.Token}:{articleKey.Biz}:{articleKey.Mid}:{articleKey.Idx}"
                : url.Trim();

            if (seen.Add(key))
            {
                result.Add(url.Trim());
            }
        }

        return result;
    }

    /// <summary>
    /// 更新文章状态
    /// </summary>
    private async Task UpdateArticleStatusAsync(TaskStatusResponseDto taskStatus)
    {
        foreach (var articleDto in taskStatus.Articles)
        {
            var dbArticle = await _context.WechatArticles
                .FirstOrDefaultAsync(a => a.TaskId == taskStatus.TaskId && a.ArticleUniqueId == articleDto.ArticleId);
            
            if (dbArticle != null)
            {
                dbArticle.Title = articleDto.Title;
                dbArticle.Author = articleDto.Author;
                dbArticle.PublishTime = articleDto.PublishTime;
                dbArticle.HtmlFilePath = articleDto.HtmlFilePath;
                dbArticle.PdfPath = articleDto.PdfFilePath;
                dbArticle.ImagesCount = articleDto.ImagesCount;
                dbArticle.VideosCount = articleDto.VideosCount;
                dbArticle.Status = articleDto.Status;
                dbArticle.ErrorMessage = articleDto.ErrorMessage;
                
                if (articleDto.Status == "completed" || articleDto.Status == "failed")
                {
                    dbArticle.CompletedAt = DateTime.UtcNow;
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 获取用户的文章列表
    /// </summary>
    public async Task<List<WechatArticle>> GetUserArticlesAsync(string userId, int page = 1, int pageSize = 20)
    {
        return await _context.WechatArticles
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// 获取用户文章总数
    /// </summary>
    public async Task<int> GetUserArticleCountAsync(string userId)
    {
        return await _context.WechatArticles
            .Where(a => a.UserId == userId)
            .CountAsync();
    }

    /// <summary>
    /// 获取单篇文章
    /// </summary>
    public async Task<WechatArticle?> GetArticleAsync(long articleId, string userId)
    {
        return await _context.WechatArticles
            .FirstOrDefaultAsync(a => a.ArticleId == articleId && a.UserId == userId);
    }

    /// <summary>
    /// 重试失败的文章爬取任务
    /// </summary>
    public async Task<(bool success, string? newTaskId, string? error)> RetryArticleAsync(long articleId, string userId)
    {
        var article = await _context.WechatArticles
            .FirstOrDefaultAsync(a => a.ArticleId == articleId && a.UserId == userId);

        if (article == null)
        {
            return (false, null, "文章不存在");
        }

        // 只能重试失败或处理中超时的文章
        if (article.Status != "failed" && article.Status != "processing")
        {
            return (false, null, $"当前状态 [{article.Status}] 不支持重试");
        }

        // 如果是 processing 状态，检查是否已经超时（超过10分钟）
        if (article.Status == "processing")
        {
            var timeSinceCreated = DateTime.UtcNow - article.CreatedAt;
            if (timeSinceCreated.TotalMinutes < 10)
            {
                return (false, null, "任务正在处理中，请耐心等待");
            }
        }

        // 删除旧的文件夹（如果存在）
        if (!string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            var folderPath = Path.Combine(_env.WebRootPath, _options.OutputPath, userId, article.ArticleUniqueId);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, recursive: true);
                    _logger.LogInformation("已删除旧文章文件夹: {Path}", folderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除旧文件夹失败: {Path}", folderPath);
                }
            }
        }

        // 重新提交爬取任务
        var (success, taskId, error) = await SubmitScrapeTaskAsync(userId, new List<string> { article.SourceUrl });

        if (!success)
        {
            return (false, null, error);
        }

        // 删除旧记录（新任务会创建新记录）
        _context.WechatArticles.Remove(article);
        await _context.SaveChangesAsync();

        _logger.LogInformation("重试文章成功: 旧ArticleId={OldId}, 新TaskId={NewTaskId}", articleId, taskId);

        return (true, taskId, null);
    }

    /// <summary>
    /// 删除文章
    /// </summary>
    public async Task<bool> DeleteArticleAsync(long articleId, string userId)
    {
        var article = await _context.WechatArticles
            .FirstOrDefaultAsync(a => a.ArticleId == articleId && a.UserId == userId);
        
        if (article == null)
        {
            return false;
        }

        // 删除文件夹
        if (!string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            var folderPath = Path.Combine(_env.WebRootPath, _options.OutputPath, userId, article.ArticleUniqueId);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, recursive: true);
                    _logger.LogInformation("已删除文章文件夹: {Path}", folderPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "删除文章文件夹失败: {Path}", folderPath);
                }
            }
        }

        _context.WechatArticles.Remove(article);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// 获取文章预览 URL
    /// </summary>
    public string? GetPreviewUrl(WechatArticle article)
    {
        if (string.IsNullOrEmpty(article.ArticleUniqueId) || string.IsNullOrEmpty(article.HtmlFilePath))
        {
            return null;
        }

        // 构建相对 URL: /wechat-articles/{userId}/{articleId}/{htmlFile}
        string EncodeSegment(string segment) => Uri.EscapeDataString(segment);
        return $"/{_options.OutputPath}/{EncodeSegment(article.UserId)}/{EncodeSegment(article.ArticleUniqueId)}/{EncodeSegment(article.HtmlFilePath)}";
    }

    /// <summary>
    /// 获取文章 PDF URL
    /// </summary>
    public string? GetPdfUrl(WechatArticle article)
    {
        if (string.IsNullOrEmpty(article.ArticleUniqueId) || string.IsNullOrEmpty(article.PdfPath))
        {
            return null;
        }

        // 构建相对 URL: /wechat-articles/{userId}/{articleId}/{pdfFile}
        string EncodeSegment(string segment) => Uri.EscapeDataString(segment);
        return $"/{_options.OutputPath}/{EncodeSegment(article.UserId)}/{EncodeSegment(article.ArticleUniqueId)}/{EncodeSegment(article.PdfPath)}";
    }

    /// <summary>
    /// 获取文章 HTML 物理路径
    /// </summary>
    public string? GetArticlePhysicalPath(WechatArticle article)
    {
        if (string.IsNullOrEmpty(article.ArticleUniqueId) || string.IsNullOrEmpty(article.HtmlFilePath))
        {
            return null;
        }

        return Path.Combine(
            _env.WebRootPath,
            _options.OutputPath,
            article.UserId,
            article.ArticleUniqueId,
            article.HtmlFilePath);
    }

    /// <summary>
    /// 获取文章目录路径（不含具体文件名）
    /// </summary>
    public string? GetArticleDirectoryPath(WechatArticle article)
    {
        if (string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            return null;
        }

        return Path.Combine(
            _env.WebRootPath,
            _options.OutputPath,
            article.UserId,
            article.ArticleUniqueId);
    }

    /// <summary>
    /// 创建 ZIP 下载包
    /// </summary>
    public async Task<(bool success, string? zipPath, string? error)> CreateDownloadZipAsync(
        long articleId, 
        string userId)
    {
        var article = await GetArticleAsync(articleId, userId);
        if (article == null)
        {
            return (false, null, "文章不存在");
        }

        if (string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            return (false, null, "文章尚未完成爬取");
        }

        var folderPath = Path.Combine(_env.WebRootPath, _options.OutputPath, userId, article.ArticleUniqueId);
        if (!Directory.Exists(folderPath))
        {
            return (false, null, "文章文件夹不存在");
        }

        try
        {
            var zipFileName = $"{article.ArticleUniqueId}.zip";
            var zipPath = Path.Combine(Path.GetTempPath(), zipFileName);
            
            // 如果已存在则删除
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            System.IO.Compression.ZipFile.CreateFromDirectory(folderPath, zipPath);
            
            return (true, zipPath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建 ZIP 文件失败");
            return (false, null, $"创建下载包失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消/终止任务（调用 Python 服务）
    /// </summary>
    public async Task<(bool success, string? error, List<string> stoppedArticles, List<string> deletedDirs)> CancelTaskAsync(string taskId, bool deleteFiles = true)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/task/{taskId}/cancel?delete_files={deleteFiles.ToString().ToLower()}", null);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("取消任务失败: {TaskId}, {StatusCode}, {Error}", taskId, response.StatusCode, errorContent);
                return (false, $"服务错误: {response.StatusCode}", new(), new());
            }

            var result = await response.Content.ReadFromJsonAsync<CancelTaskResponseDto>();
            if (result == null)
            {
                return (false, "解析响应失败", new(), new());
            }

            return (result.Success, null, result.StoppedArticles, result.DeletedDirs);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "无法连接到爬虫服务");
            return (false, "无法连接到爬虫服务", new(), new());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消任务失败: {TaskId}", taskId);
            return (false, $"取消失败: {ex.Message}", new(), new());
        }
    }

    /// <summary>
    /// 删除文章及相关文件（带日志记录）
    /// </summary>
    public async Task<(bool success, string? error, string? logDetails)> DeleteArticleWithLogAsync(long articleId, string userId, string? clientIp = null)
    {
        var article = await _context.WechatArticles
            .FirstOrDefaultAsync(a => a.ArticleId == articleId && a.UserId == userId);
        
        if (article == null)
        {
            return (false, "文章不存在", null);
        }

        var logDetails = new Dictionary<string, object>
        {
            ["articleId"] = articleId,
            ["taskId"] = article.TaskId ?? "",
            ["title"] = article.Title ?? "",
            ["sourceUrl"] = article.SourceUrl,
            ["status"] = article.Status
        };

        var deletedFiles = new List<string>();
        var errors = new List<string>();

        // 1. 尝试取消 Python 服务中的任务（如果还在运行）
        if (!string.IsNullOrEmpty(article.TaskId) && article.Status == "processing")
        {
            var (cancelSuccess, cancelError, stoppedArticles, deletedDirs) = await CancelTaskAsync(article.TaskId, false);
            logDetails["cancelResult"] = new { cancelSuccess, cancelError, stoppedArticles };
            if (!cancelSuccess && !string.IsNullOrEmpty(cancelError))
            {
                errors.Add($"取消任务: {cancelError}");
            }
        }

        // 2. 删除文件夹
        if (!string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            var folderPath = Path.Combine(_env.WebRootPath, _options.OutputPath, userId, article.ArticleUniqueId);
            if (Directory.Exists(folderPath))
            {
                try
                {
                    Directory.Delete(folderPath, recursive: true);
                    deletedFiles.Add(folderPath);
                    _logger.LogInformation("已删除文章文件夹: {Path}", folderPath);
                }
                catch (Exception ex)
                {
                    errors.Add($"删除文件夹失败: {ex.Message}");
                    _logger.LogWarning(ex, "删除文章文件夹失败: {Path}", folderPath);
                }
            }
        }

        logDetails["deletedFiles"] = deletedFiles;

        // 3. 删除数据库记录
        _context.WechatArticles.Remove(article);
        await _context.SaveChangesAsync();

        // 4. 写入审计日志
        var log = new WechatScrapeLog
        {
            UserId = userId,
            Action = "DeleteArticle",
            TargetId = articleId.ToString(),
            Details = System.Text.Json.JsonSerializer.Serialize(logDetails),
            Status = errors.Count == 0 ? "Success" : "Partial",
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null,
            ClientIp = clientIp,
            CreatedAt = DateTime.UtcNow
        };
        _context.WechatScrapeLogs.Add(log);
        await _context.SaveChangesAsync();

        var logJson = System.Text.Json.JsonSerializer.Serialize(logDetails);
        return (true, errors.Count > 0 ? string.Join("; ", errors) : null, logJson);
    }

    /// <summary>
    /// 获取任务管理列表（包含数据库ID和Python Task ID）
    /// </summary>
    public async Task<List<WechatArticle>> GetTaskManagementListAsync(string userId, int page = 1, int pageSize = 50)
    {
        return await _context.WechatArticles
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// 获取任务管理列表总数
    /// </summary>
    public async Task<int> GetTaskManagementCountAsync(string userId)
    {
        return await _context.WechatArticles
            .Where(a => a.UserId == userId)
            .CountAsync();
    }

    /// <summary>
    /// 写入审计日志
    /// </summary>
    public async Task WriteLogAsync(string userId, string action, string? targetId, object? details, string status = "Success", string? errorMessage = null, string? clientIp = null)
    {
        var log = new WechatScrapeLog
        {
            UserId = userId,
            Action = action,
            TargetId = targetId,
            Details = details != null ? System.Text.Json.JsonSerializer.Serialize(details) : null,
            Status = status,
            ErrorMessage = errorMessage,
            ClientIp = clientIp,
            CreatedAt = DateTime.UtcNow
        };
        _context.WechatScrapeLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
