using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

public sealed class ArticleExtractionService(
    VaultDbContext db,
    ArticleScraperService scraper,
    SecretCipher cipher,
    IHttpClientFactory clients,
    ILogger<ArticleExtractionService> logger)
{
    public async Task<(bool Success, long? ExtractionId, string? Error)> ExtractAsync(long articleId, string ownerId, string prompt, CancellationToken cancellationToken)
    {
        prompt = prompt.Trim();
        if (prompt.Length is < 2 or > 4000) return (false, null, "萃取要求应为 2–4000 个字符。");
        var article = await db.KnowledgeArticles.SingleOrDefaultAsync(x => x.Id == articleId && x.OwnerId == ownerId, cancellationToken);
        if (article is null) return (false, null, "文章不存在。");
        if (article.Status != "completed") return (false, null, "文章尚未抓取完成。");
        var settings = await db.ArticleAiSettings.SingleOrDefaultAsync(x => x.OwnerId == ownerId, cancellationToken);
        if (settings is null) return (false, null, "请先配置用于萃取的 AI 模型。");
        var htmlPath = scraper.GetHtmlPath(article);
        if (htmlPath is null || !File.Exists(htmlPath)) return (false, null, "文章正文文件不存在，请重新抓取。");

        var extraction = new ArticleExtraction { ArticleId = articleId, OwnerId = ownerId, Prompt = prompt, ModelUsed = settings.ModelName };
        db.ArticleExtractions.Add(extraction);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var html = await File.ReadAllTextAsync(htmlPath, cancellationToken);
            var text = ExtractText(html);
            if (text.Length < 20) throw new InvalidOperationException("文章正文为空。");
            var apiKey = cipher.DecryptValue($"article-ai-key:{ownerId}", ToPayload(settings));
            try
            {
                extraction.Result = await CallCompatibleApiAsync(settings, apiKey, prompt, text, cancellationToken);
            }
            finally
            {
                // Managed strings cannot be reliably zeroed; keep the decrypted key scoped to this call only.
                apiKey = string.Empty;
            }
            extraction.Status = "completed";
            extraction.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return (true, extraction.Id, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Article extraction failed for article {ArticleId}", articleId);
            extraction.Status = "failed";
            extraction.ErrorMessage = FriendlyError(ex);
            extraction.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return (false, extraction.Id, extraction.ErrorMessage);
        }
    }

    public static EncryptedPayload ToPayload(ArticleAiSettings settings) => new(
        settings.ApiKeyCiphertext, settings.ApiKeyNonce, settings.ApiKeyAuthenticationTag,
        settings.WrappedDataKey, settings.KeyWrapNonce, settings.KeyWrapAuthenticationTag);

    private async Task<string> CallCompatibleApiAsync(ArticleAiSettings settings, string apiKey, string prompt, string articleText, CancellationToken cancellationToken)
    {
        var endpoint = ValidateBaseUrl(settings.BaseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var body = new
        {
            model = settings.ModelName,
            messages = new object[]
            {
                new { role = "system", content = "你是严谨的中文文章分析助手。只依据给出的文章，区分事实、观点和推断；信息不足时明确说明。使用清晰的 Markdown 输出。" },
                new { role = "user", content = $"萃取要求：\n{prompt}\n\n文章正文：\n{articleText}" }
            },
            stream = false
        };
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var client = clients.CreateClient(nameof(ArticleExtractionService));
        client.Timeout = TimeSpan.FromMinutes(5);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"AI 服务返回 {(int)response.StatusCode}。", null, response.StatusCode);
        using var json = JsonDocument.Parse(responseText);
        if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
            throw new InvalidOperationException("AI 服务返回格式不兼容。");
        return content.GetString()?.Trim() ?? string.Empty;
    }

    private static Uri ValidateBaseUrl(string raw)
    {
        if (!Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("AI Base URL 必须是有效的 HTTPS 地址。");
        if (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("AI Base URL 不允许指向本机。");
        return uri;
    }

    private static string ExtractText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript|//svg") ?? Enumerable.Empty<HtmlNode>()) node.Remove();
        var content = doc.DocumentNode.SelectSingleNode("//*[@id='js_content']") ?? doc.DocumentNode.SelectSingleNode("//article") ?? doc.DocumentNode;
        var decoded = System.Net.WebUtility.HtmlDecode(content.InnerText);
        var lines = decoded.Split('\n').Select(x => string.Join(' ', x.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))).Where(x => x.Length > 0);
        return string.Join('\n', lines).Trim()[..Math.Min(120_000, string.Join('\n', lines).Trim().Length)];
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized } => "AI API Key 无效或已失效。",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "AI 服务额度不足或请求过于频繁。",
        TaskCanceledException => "AI 服务响应超时。",
        _ => ex.Message.Length > 300 ? ex.Message[..300] : ex.Message
    };
}
