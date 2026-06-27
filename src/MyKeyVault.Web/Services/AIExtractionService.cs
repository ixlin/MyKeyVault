using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Services;

/// <summary>
/// AI 萃取服务
/// </summary>
public class AIExtractionService
{
    private readonly ApplicationDbContext _context;
    private readonly WechatScraperService _scraperService;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AIExtractionService> _logger;
    private readonly HttpClient _httpClient;

    public AIExtractionService(
        ApplicationDbContext context,
        WechatScraperService scraperService,
        IWebHostEnvironment env,
        ILogger<AIExtractionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _scraperService = scraperService;
        _env = env;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // 增加超时时间
    }

    /// <summary>
    /// 获取用户的 AI 配置
    /// </summary>
    public async Task<AIConfig?> GetUserConfigAsync(string userId)
    {
        return await _context.AIConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    /// <summary>
    /// 保存或更新 AI 配置
    /// </summary>
    public async Task<(bool success, string? error)> SaveConfigAsync(string userId, AIConfig config)
    {
        try
        {
            var existing = await _context.AIConfigs
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (existing != null)
            {
                existing.Provider = config.Provider;
                existing.ApiKey = config.ApiKey;
                existing.BaseUrl = config.BaseUrl;
                existing.ModelName = config.ModelName;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                config.UserId = userId;
                config.CreatedAt = DateTime.UtcNow;
                config.UpdatedAt = DateTime.UtcNow;
                _context.AIConfigs.Add(config);
            }

            await _context.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存 AI 配置失败");
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 执行文章萃取
    /// </summary>
    public async Task<(bool success, WechatArticleExtraction? extraction, string? error)> ExtractArticleAsync(
        long articleId,
        string userId,
        string prompt)
    {
        try
        {
            // 检查文章是否存在
            var article = await _context.WechatArticles
                .FirstOrDefaultAsync(a => a.ArticleId == articleId && a.UserId == userId);

            if (article == null)
            {
                return (false, null, "文章不存在");
            }

            if (article.Status != "completed")
            {
                return (false, null, "文章尚未完成下载");
            }

            // 获取 AI 配置
            var config = await GetUserConfigAsync(userId);
            if (config == null)
            {
                return (false, null, "请先配置 AI 设置");
            }

            // 创建萃取记录
            var extraction = new WechatArticleExtraction
            {
                ArticleId = articleId,
                UserId = userId,
                Prompt = prompt,
                ModelUsed = config.ModelName,
                Status = "processing",
                CreatedAt = DateTime.UtcNow
            };

            _context.WechatArticleExtractions.Add(extraction);
            await _context.SaveChangesAsync();

            try
            {
                // 读取文章 HTML 内容
                var htmlContent = await ReadArticleHtmlAsync(article);
                if (string.IsNullOrEmpty(htmlContent))
                {
                    throw new Exception("无法读取文章内容");
                }

                // 提取文章正文和图片
                var (textContent, images) = ExtractContentAndImages(htmlContent, article);

                // 调用 AI API
                var result = await CallAIApiAsync(config, prompt, textContent, images);

                // 保存结果
                extraction.Result = result;
                extraction.Status = "completed";
                extraction.CompletedAt = DateTime.UtcNow;

                // 保存到文件
                var filePath = await SaveExtractionResultAsync(article, extraction, result);
                extraction.FilePath = filePath;

                await _context.SaveChangesAsync();

                return (true, extraction, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "萃取失败: ArticleId={ArticleId}", articleId);
                extraction.Status = "failed";
                extraction.ErrorMessage = ex.Message;
                extraction.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return (false, extraction, ex.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建萃取记录失败");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// 读取文章 HTML 内容
    /// </summary>
    private async Task<string?> ReadArticleHtmlAsync(WechatArticle article)
    {
        var physicalPath = _scraperService.GetArticlePhysicalPath(article);
        
        // 如果 HtmlFilePath 有值且文件存在，直接读取
        if (!string.IsNullOrEmpty(physicalPath) && File.Exists(physicalPath))
        {
            return await File.ReadAllTextAsync(physicalPath);
        }

        // 回退：HtmlFilePath 为空时，扫描文章目录查找 HTML 文件
        if (!string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            var articleDir = Path.Combine(
                _env.WebRootPath,
                "wechat-articles",
                article.UserId,
                article.ArticleUniqueId);

            if (Directory.Exists(articleDir))
            {
                var htmlFiles = Directory.GetFiles(articleDir, "*.html");
                if (htmlFiles.Length > 0)
                {
                    _logger.LogInformation("回退查找 HTML: {Dir}, 找到 {Count} 个文件", articleDir, htmlFiles.Length);
                    return await File.ReadAllTextAsync(htmlFiles[0]);
                }
            }
        }

        _logger.LogWarning("无法读取文章内容: ArticleId={ArticleId}, HtmlFilePath={HtmlFilePath}, ArticleUniqueId={ArticleUniqueId}",
            article.ArticleId, article.HtmlFilePath, article.ArticleUniqueId);
        return null;
    }

    /// <summary>
    /// 提取文章正文和图片
    /// </summary>
    private (string textContent, List<string> imageBase64List) ExtractContentAndImages(
        string htmlContent,
        WechatArticle article)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        // 提取正文（移除脚本和样式）
        var scripts = doc.DocumentNode.SelectNodes("//script");
        if (scripts != null)
        {
            foreach (var script in scripts)
            {
                script.Remove();
            }
        }

        var styles = doc.DocumentNode.SelectNodes("//style");
        if (styles != null)
        {
            foreach (var style in styles)
            {
                style.Remove();
            }
        }

        var textContent = doc.DocumentNode.InnerText;

        // 提取图片并转换为 base64
        var imageBase64List = new List<string>();
        var imgNodes = doc.DocumentNode.SelectNodes("//img[@src]");

        if (imgNodes != null && article.ArticleUniqueId != null)
        {
            var articleDir = Path.Combine(
                _env.WebRootPath,
                "wechat-articles",
                article.UserId,
                article.ArticleUniqueId);

            foreach (var img in imgNodes.Take(10)) // 限制最多10张图片
            {
                try
                {
                    var src = img.GetAttributeValue("src", "");
                    if (string.IsNullOrEmpty(src)) continue;

                    // 处理相对路径
                    var imagePath = src;
                    if (!Path.IsPathRooted(src))
                    {
                        imagePath = Path.Combine(articleDir, src);
                    }

                    if (File.Exists(imagePath))
                    {
                        var imageBytes = File.ReadAllBytes(imagePath);
                        var base64 = Convert.ToBase64String(imageBytes);
                        var extension = Path.GetExtension(imagePath).ToLower();
                        var mimeType = extension switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".png" => "image/png",
                            ".gif" => "image/gif",
                            ".webp" => "image/webp",
                            _ => "image/jpeg"
                        };
                        imageBase64List.Add($"data:{mimeType};base64,{base64}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取图片失败: {Src}", img.GetAttributeValue("src", ""));
                }
            }
        }

        return (textContent, imageBase64List);
    }

    /// <summary>
    /// 调用 AI API
    /// </summary>
    private async Task<string> CallAIApiAsync(
        AIConfig config,
        string prompt,
        string textContent,
        List<string> imageBase64List)
    {
        var baseUrl = string.IsNullOrEmpty(config.BaseUrl)
            ? "https://api.deepseek.com"
            : config.BaseUrl.TrimEnd('/');

        var endpoint = $"{baseUrl}/chat/completions";

        // 构建消息
        var messages = new List<object>();

        // 系统消息
        messages.Add(new
        {
            role = "system",
            content = "你是一个专业的内容分析助手，擅长提炼和总结文章要点。请按照用户的要求，对提供的文章内容进行分析和萃取。"
        });

        // 用户消息（DeepSeek 当前版本暂不支持视觉功能，只发送文本）
        // TODO: 未来 DeepSeek 支持视觉功能后，可以添加图片支持
        var imageInfo = imageBase64List.Count > 0 
            ? $"\n\n[提示：文章包含 {imageBase64List.Count} 张图片，但当前模型暂不支持图片分析]" 
            : "";

        messages.Add(new
        {
            role = "user",
            content = $"{prompt}\n\n文章内容：\n{textContent}{imageInfo}"
        });

        var requestBody = new
        {
            model = config.ModelName,
            messages,
            stream = false
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        _logger.LogInformation("调用 AI API: {Endpoint}, Model: {Model}", endpoint, config.ModelName);

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI API 调用失败: {StatusCode}, {Response}", response.StatusCode, responseContent);
            throw new Exception($"AI API 调用失败: {response.StatusCode} - {responseContent}");
        }

        // 解析响应
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new Exception("AI API 返回格式错误");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            throw new Exception("AI API 返回格式错误");
        }

        return content.GetString() ?? string.Empty;
    }

    /// <summary>
    /// 保存萃取结果到文件
    /// </summary>
    private async Task<string> SaveExtractionResultAsync(
        WechatArticle article,
        WechatArticleExtraction extraction,
        string result)
    {
        if (string.IsNullOrEmpty(article.ArticleUniqueId))
        {
            throw new Exception("文章标识为空");
        }

        var articleDir = Path.Combine(
            _env.WebRootPath,
            "wechat-articles",
            article.UserId,
            article.ArticleUniqueId);

        Directory.CreateDirectory(articleDir);

        // 生成文件名（去除特殊字符）
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeTitle = SanitizeFileName(article.Title ?? "未命名");
        var fileName = $"extraction_{safeTitle}_{timestamp}.md";
        var filePath = Path.Combine(articleDir, fileName);

        // 保存为 Markdown 文件
        var fileContent = $"# 萃取结果\n\n" +
                         $"**文章标题**: {article.Title}\n\n" +
                         $"**萃取时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                         $"**提示词**:\n```\n{extraction.Prompt}\n```\n\n" +
                         $"---\n\n" +
                         $"{result}\n";

        await File.WriteAllTextAsync(filePath, fileContent);

        // 返回相对路径
        return fileName;
    }

    /// <summary>
    /// 清理文件名中的特殊字符
    /// </summary>
    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        // 限制长度
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return sanitized;
    }

    /// <summary>
    /// 获取文章的萃取历史
    /// </summary>
    public async Task<List<WechatArticleExtraction>> GetArticleExtractionsAsync(long articleId, string userId)
    {
        return await _context.WechatArticleExtractions
            .Where(e => e.ArticleId == articleId && e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 获取萃取详情
    /// </summary>
    public async Task<WechatArticleExtraction?> GetExtractionAsync(long extractionId, string userId)
    {
        return await _context.WechatArticleExtractions
            .Include(e => e.Article)
            .FirstOrDefaultAsync(e => e.ExtractionId == extractionId && e.UserId == userId);
    }

    /// <summary>
    /// 获取萃取文件的物理路径
    /// </summary>
    public string? GetExtractionFilePath(WechatArticleExtraction extraction)
    {
        if (extraction.Article == null || 
            string.IsNullOrEmpty(extraction.Article.ArticleUniqueId) ||
            string.IsNullOrEmpty(extraction.FilePath))
        {
            return null;
        }

        return Path.Combine(
            _env.WebRootPath,
            "wechat-articles",
            extraction.UserId,
            extraction.Article.ArticleUniqueId,
            extraction.FilePath);
    }
}
