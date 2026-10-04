using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

public sealed class ArticleScraperOptions
{
    public const string SectionName = "ArticleScraper";
    public string ServiceUrl { get; set; } = "http://127.0.0.1:5001";
    public int MaxUrlsPerRequest { get; set; } = 10;
}

public sealed class ArticleScraperService(
    VaultDbContext db,
    IHttpClientFactory clients,
    IWebHostEnvironment environment,
    Microsoft.Extensions.Options.IOptions<ArticleScraperOptions> options,
    ILogger<ArticleScraperService> logger)
{
    private readonly ArticleScraperOptions _options = options.Value;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Client().GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    public async Task<(bool Success, string? Error)> SubmitAsync(string ownerId, IEnumerable<string> rawUrls, CancellationToken cancellationToken)
    {
        var urls = rawUrls.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (urls.Count is < 1) return (false, "请至少粘贴一个微信公众号文章链接。");
        if (urls.Count > Math.Clamp(_options.MaxUrlsPerRequest, 1, 10)) return (false, $"每次最多抓取 {Math.Clamp(_options.MaxUrlsPerRequest, 1, 10)} 篇文章。");
        if (urls.Any(x => !IsWechatArticleUrl(x))) return (false, "只接受 https://mp.weixin.qq.com 的文章链接。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"article-submit:" + ownerId}, 0))", cancellationToken);
        var candidates = await db.KnowledgeArticles.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && urls.Contains(x.SourceUrl) && (x.Status == "completed" || x.Status == "pending" || x.Status == "processing"))
            .ToListAsync(cancellationToken);
        var existing = candidates.Where(x => x.Status != "completed" || GetPreviewUrl(x) is not null).Select(x => x.SourceUrl);
        urls = urls.Except(existing, StringComparer.OrdinalIgnoreCase).ToList();
        if (urls.Count == 0) return (false, "这些文章已经在资料库中，或正在抓取中。");
        if (await db.KnowledgeArticles.CountAsync(x => x.OwnerId == ownerId && (x.Status == "pending" || x.Status == "processing"), cancellationToken) + urls.Count > 10)
            return (false, "请等待当前抓取任务完成后再添加文章。");

        var outputBase = Path.Combine(environment.WebRootPath, "wechat-articles");
        Directory.CreateDirectory(outputBase);
        ScrapeResponse? result;
        try
        {
            using var response = await Client().PostAsJsonAsync("/api/scrape", new { urls, output_base = outputBase, user_id = ownerId }, cancellationToken);
            if (!response.IsSuccessStatusCode) return (false, $"抓取服务返回 {(int)response.StatusCode}，请稍后重试。");
            result = await response.Content.ReadFromJsonAsync<ScrapeResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Article scraper unavailable");
            return (false, "抓取服务当前不可用。");
        }

        if (result is null || result.Articles.Count != urls.Count) return (false, "抓取服务返回了无效任务。");
        foreach (var item in result.Articles)
        {
            db.KnowledgeArticles.Add(new KnowledgeArticle
            {
                OwnerId = ownerId,
                SourceUrl = item.SourceUrl,
                StorageKey = item.ArticleId,
                TaskId = result.TaskId,
                Status = item.Status,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (true, null);
    }

    public async Task SyncActiveAsync(string ownerId, CancellationToken cancellationToken)
    {
        var taskIds = await db.KnowledgeArticles
            .Where(x => x.OwnerId == ownerId && x.TaskId != null && (x.Status == "pending" || x.Status == "processing"))
            .Select(x => x.TaskId!).Distinct().Take(20).ToListAsync(cancellationToken);
        foreach (var taskId in taskIds)
        {
            TaskStatusResponse? status;
            try
            {
                using var response = await Client().GetAsync($"/api/task/{Uri.EscapeDataString(taskId)}", cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    await db.KnowledgeArticles.Where(x => x.OwnerId == ownerId && x.TaskId == taskId && (x.Status == "pending" || x.Status == "processing"))
                        .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "failed").SetProperty(x => x.Stage, "任务已中断")
                            .SetProperty(x => x.ErrorMessage, "抓取服务重启或任务已过期，可以重新抓取。").SetProperty(x => x.CompletedAtUtc, DateTime.UtcNow), cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode) continue;
                status = await response.Content.ReadFromJsonAsync<TaskStatusResponse>(cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogDebug(ex, "Could not synchronize scrape task {TaskId}", taskId);
                break;
            }
            if (status is null) continue;
            var articles = await db.KnowledgeArticles.Where(x => x.OwnerId == ownerId && x.TaskId == taskId).ToListAsync(cancellationToken);
            foreach (var article in articles)
            {
                var update = status.Articles.FirstOrDefault(x => x.ArticleId == article.StorageKey);
                if (update is null) continue;
                if ((article.Status is "completed" or "failed" or "cancelled") && (update.Status is "pending" or "processing")) continue;
                article.Title = Limit(update.Title, 240);
                article.Author = Limit(update.Author, 120);
                article.PublishedText = Limit(update.PublishTime, 80);
                article.HtmlFileName = SafeFileName(update.HtmlFilePath);
                article.PdfFileName = SafeFileName(update.PdfFilePath);
                article.ImagesCount = update.ImagesCount;
                article.VideosCount = update.VideosCount;
                article.Status = update.Status;
                article.ErrorMessage = Limit(update.ErrorMessage, 600);
                article.Progress = Math.Clamp(update.Progress, 0, 100);
                article.Stage = Limit(update.Stage, 300) ?? "等待开始";
                if (update.Logs.Count > 0)
                    article.ProcessLogJson = JsonSerializer.Serialize(update.Logs.TakeLast(60));
                if (update.Status is "completed" or "failed" or "cancelled") article.CompletedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<(bool Success, string? Error)> RetryAsync(long id, string ownerId, CancellationToken cancellationToken)
    {
        var article = await db.KnowledgeArticles.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);
        if (article is null) return (false, "文章不存在。");
        if (article.Status is not ("failed" or "cancelled")) return (false, "只能重试失败或取消的任务。");
        return await SubmitAsync(ownerId, new[] { article.SourceUrl }, cancellationToken);
    }

    public static IReadOnlyList<ProgressEntry> ReadLog(string json)
    {
        try { return JsonSerializer.Deserialize<List<ProgressEntry>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<ProgressEntry>(); }
    }

    private static string? Limit(string? value, int length) => value is null ? null : value[..Math.Min(value.Length, length)];

    public string? GetPreviewUrl(KnowledgeArticle article, bool pdf = false)
    {
        var file = pdf ? article.PdfFileName : article.HtmlFileName;
        if (string.IsNullOrWhiteSpace(article.StorageKey) || string.IsNullOrWhiteSpace(file)) return null;
        var physicalPath = SafeArticlePath(article, file);
        if (physicalPath is null) return null;
        if (!File.Exists(physicalPath)) return null;
        return $"/wechat-articles/{Uri.EscapeDataString(article.OwnerId)}/{Uri.EscapeDataString(article.StorageKey)}/{Uri.EscapeDataString(file)}";
    }

    public string? GetHtmlPath(KnowledgeArticle article)
    {
        if (string.IsNullOrWhiteSpace(article.StorageKey) || string.IsNullOrWhiteSpace(article.HtmlFileName)) return null;
        return SafeArticlePath(article, article.HtmlFileName);
    }

    private string? SafeArticlePath(KnowledgeArticle article, string file)
    {
        if (string.IsNullOrEmpty(article.StorageKey) || Path.GetFileName(file) != file || file is "." or ".."
            || article.StorageKey is "." or ".." || Path.GetFileName(article.StorageKey) != article.StorageKey
            || article.OwnerId is "." or ".." || Path.GetFileName(article.OwnerId) != article.OwnerId) return null;
        return Path.Combine(environment.WebRootPath, "wechat-articles", article.OwnerId, article.StorageKey, file);
    }

    private HttpClient Client()
    {
        var client = clients.CreateClient(nameof(ArticleScraperService));
        client.BaseAddress = new Uri(_options.ServiceUrl);
        client.Timeout = TimeSpan.FromSeconds(12);
        return client;
    }

    private static bool IsWechatArticleUrl(string value) =>
        value.Length <= 500 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && uri.Host.Equals("mp.weixin.qq.com", StringComparison.OrdinalIgnoreCase) && (uri.AbsolutePath == "/s" || uri.AbsolutePath.StartsWith("/s/", StringComparison.Ordinal));

    private static string? SafeFileName(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);

    private sealed class ScrapeResponse
    {
        [JsonPropertyName("task_id")] public string TaskId { get; set; } = string.Empty;
        [JsonPropertyName("articles")] public List<ArticleResult> Articles { get; set; } = new();
    }
    private sealed class TaskStatusResponse
    {
        [JsonPropertyName("articles")] public List<ArticleResult> Articles { get; set; } = new();
    }
    private sealed class ArticleResult
    {
        [JsonPropertyName("article_id")] public string ArticleId { get; set; } = string.Empty;
        [JsonPropertyName("source_url")] public string SourceUrl { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("author")] public string? Author { get; set; }
        [JsonPropertyName("publish_time")] public string? PublishTime { get; set; }
        [JsonPropertyName("html_file_path")] public string? HtmlFilePath { get; set; }
        [JsonPropertyName("pdf_file_path")] public string? PdfFilePath { get; set; }
        [JsonPropertyName("images_count")] public int ImagesCount { get; set; }
        [JsonPropertyName("videos_count")] public int VideosCount { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "pending";
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
        [JsonPropertyName("progress")] public int Progress { get; set; }
        [JsonPropertyName("stage")] public string? Stage { get; set; }
        [JsonPropertyName("logs")] public List<ProgressEntry> Logs { get; set; } = new();
    }
    public sealed record ProgressEntry(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("progress")] int Progress,
        [property: JsonPropertyName("message")] string Message);
}
