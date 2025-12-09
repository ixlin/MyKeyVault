using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";
    
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 微信爬虫服务
/// </summary>
public class WechatScraperService
{
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
            var response = await _httpClient.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python 爬虫服务不可用");
            return false;
        }
    }

    /// <summary>
    /// 提交爬取任务
    /// </summary>
    public async Task<(bool success, string? taskId, string? error)> SubmitScrapeTaskAsync(
        string userId, 
        List<string> urls)
    {
        if (urls.Count > _options.MaxArticlesPerRequest)
        {
            return (false, null, $"每次最多爬取 {_options.MaxArticlesPerRequest} 篇文章");
        }

        // 验证链接
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

            // 保存文章记录到数据库
            foreach (var article in result.Articles)
            {
                var dbArticle = new WechatArticle
                {
                    UserId = userId,
                    SourceUrl = article.SourceUrl,
                    ArticleUniqueId = article.ArticleId,
                    TaskId = result.TaskId,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.WechatArticles.Add(dbArticle);
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation("爬取任务已提交: TaskId={TaskId}, 文章数={Count}", result.TaskId, result.TotalCount);

            return (true, result.TaskId, null);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务状态失败: {TaskId}", taskId);
            return null;
        }
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
}
