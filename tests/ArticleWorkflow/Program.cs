using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

var connection = Environment.GetEnvironmentVariable("ARTICLE_TEST_DB") ?? throw new Exception("ARTICLE_TEST_DB required");
if (!new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!.StartsWith("articles_test_")) throw new Exception("Disposable test DB required");
await using var db = new VaultDbContext(new DbContextOptionsBuilder<VaultDbContext>().UseNpgsql(connection).Options);
var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception("FAILED " + name); checks++; Console.WriteLine("PASS " + name); }
// Exercise the real additive migration on a historical record, not EnsureCreated.
await db.GetService<IMigrator>().MigrateAsync("20260820160000_AddKnowledgeLibrary");
await db.Database.ExecuteSqlRawAsync("""
INSERT INTO "KnowledgeArticles" ("Id","OwnerId","SourceUrl","Status","ImagesCount","VideosCount","CreatedAtUtc") VALUES (100,'legacy','https://mp.weixin.qq.com/s/legacy','completed',0,0,now());
INSERT INTO "ArticleExtractions" ("Id","ArticleId","OwnerId","Prompt","Result","Status","CreatedAtUtc") VALUES (100,100,'legacy','历史问题','历史结果不变','completed',now()),(101,100,'legacy','历史问题二','历史结果二','completed',now());
""");
await db.Database.MigrateAsync();
var legacy = await db.ArticleExtractions.Where(x => x.OwnerId == "legacy").ToListAsync();
Check(legacy.Count == 2 && legacy[0].Result == "历史结果不变" && legacy.All(x => x.ConversationId != Guid.Empty) && legacy.Select(x => x.ConversationId).Distinct().Count() == 2, "migration preserves historical results as distinct conversations");
Check((await db.KnowledgeArticles.FindAsync(100L))!.Progress == 100, "historical completed progress migrated");
var root = Path.Combine(Path.GetTempPath(), "mykeyvault-articles-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "wechat-articles", "alice", "one"));
await File.WriteAllTextAsync(Path.Combine(root, "wechat-articles", "alice", "one", "article.html"), "<article>这是一篇专门用于隔离测试的文章，介绍如何提高团队效率，以及应该如何核对数据与事实。</article>");
var factory = new FakeClients();
var scraper = new ArticleScraperService(db, factory, new TestEnvironment(root), Options.Create(new ArticleScraperOptions()), NullLogger<ArticleScraperService>.Instance);
var cipher = new SecretCipher(Options.Create(new VaultEncryptionOptions { MasterKey = Convert.ToBase64String(new byte[32]) }));
var payload = cipher.EncryptValue("article-ai-key:alice", "synthetic-not-real-key");
db.ArticleAiSettings.Add(new ArticleAiSettings { OwnerId = "alice", ModelName = "deepseek-flash", ApiKeyCiphertext = payload.Ciphertext, ApiKeyNonce = payload.Nonce, ApiKeyAuthenticationTag = payload.AuthenticationTag, WrappedDataKey = payload.WrappedDataKey, KeyWrapNonce = payload.KeyWrapNonce, KeyWrapAuthenticationTag = payload.KeyWrapAuthenticationTag });
var article = new KnowledgeArticle { OwnerId = "alice", SourceUrl = "https://mp.weixin.qq.com/s/one", StorageKey = "one", HtmlFileName = "article.html", Status = "completed" };
db.KnowledgeArticles.Add(article); await db.SaveChangesAsync();
var markdown = new ArticleMarkdown();
var extraction = new ArticleExtractionService(db, scraper, cipher, factory, markdown, NullLogger<ArticleExtractionService>.Instance);
var events = new List<ArticleExtractionService.ExtractionEvent>();
Task Emit(ArticleExtractionService.ExtractionEvent e) { events.Add(e); return Task.CompletedTask; }
await extraction.RunAsync(article.Id, "alice", null, "总结文章", Emit, default);
var first = events.First(x => x.Type == "start");
Check(events.Any(x => x.Type == "delta") && events.Last().Type == "done", "incremental stream ends with saved response");
var record = await db.ArticleExtractions.FindAsync(first.Id);
Check(record!.Status == "completed" && record.Result == "## 测试回复\n**总结**内容。", "stream persisted completely");
events.Clear(); await extraction.RunAsync(article.Id, "alice", first.ConversationId, "再详细解释", Emit, default);
Check(events.Last().Type == "done" && await db.ArticleExtractions.CountAsync(x => x.ConversationId == first.ConversationId) == 2, "follow-up shares conversation");
Check(factory.LastRequest.Contains("总结文章") && factory.LastRequest.Contains("再详细解释") && factory.LastRequest.Contains("assistant"), "follow-up includes prior article conversation");
events.Clear(); await extraction.RunAsync(article.Id, "bob", first.ConversationId, "读取内容", Emit, default);
Check(events.Single().Type == "error", "other owner cannot extract article");
events.Clear(); await extraction.RunAsync(article.Id, "alice", legacy[0].ConversationId, "读取对话", Emit, default);
Check(events.Single().Type == "error", "foreign conversation rejected");
factory.Truncate = true; events.Clear(); await extraction.RunAsync(article.Id, "alice", null, "中断测试", Emit, default);
Check(events.Last().Type == "error" && (await db.ArticleExtractions.OrderByDescending(x => x.CreatedAtUtc).FirstAsync()).Result!.Length > 0, "interrupted model response preserves partial text");
factory.Truncate = false;
using (var stop = new CancellationTokenSource())
{
    events.Clear();
    await extraction.RunAsync(article.Id, "alice", null, "停止测试", e => { events.Add(e); if (e.Type == "delta") stop.Cancel(); return Task.CompletedTask; }, stop.Token);
    var stopped = await db.ArticleExtractions.FindAsync(events.First(x => x.Type == "start").Id);
    Check(stopped!.Status == "cancelled" && !string.IsNullOrEmpty(stopped.Result), "stop preserves partial response as cancelled");
}
Check(markdown.Render("# 标题\n\n**粗体** *斜体*\n\n| A | B |\n|---|---|\n| 1 | 2 |").Contains("<table>"), "Markdown headings and tables rendered");
var unsafeHtml = markdown.Render("<script>alert(1)</script>\n\n[x](javascript:alert(1))\n\n![x](https://tracker.example/x)\n\n[x](https://example.com)");
Check(!unsafeHtml.Contains("<script") && !unsafeHtml.Contains("href=\"javascript:") && !unsafeHtml.Contains("<img") && unsafeHtml.Contains("noopener noreferrer"), "Markdown strips executable markup and remote trackers");
foreach (var url in new[] { "", "http://example.com", "https://127.0.0.1", "https://user:password@example.com" })
{
    var rejected = false; try { ArticleExtractionService.ValidateBaseUrl(url); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "unsafe base URL rejected");
}
Check(!(await scraper.SubmitAsync("alice", new[] { "https://example.com/x" }, default)).Success, "non-WeChat URL rejected");
Check(!(await scraper.SubmitAsync("alice", new[] { article.SourceUrl }, default)).Success, "existing completed article not duplicated");
var source = "https://mp.weixin.qq.com/s/new-test";
Check((await scraper.SubmitAsync("alice", new[] { source }, default)).Success, "valid scrape creates task");
await scraper.SyncActiveAsync("alice", default);
var queued = await db.KnowledgeArticles.SingleAsync(x => x.SourceUrl == source);
Check(queued.Progress == 55 && queued.Stage == "下载图片" && ArticleScraperService.ReadLog(queued.ProcessLogJson).Count == 1, "progress and detailed timeline persisted");
Check(!(await scraper.RetryAsync(queued.Id, "bob", default)).Success, "other owner cannot retry task");
factory.MissingTask = true; await scraper.SyncActiveAsync("alice", default); await db.Entry(queued).ReloadAsync();
Check(queued.Status == "failed" && queued.ErrorMessage!.Contains("重启"), "lost worker task gets actionable failure");
Check(scraper.GetHtmlPath(new KnowledgeArticle { OwnerId = "alice", StorageKey = "..", HtmlFileName = "article.html" }) is null, "unsafe storage path rejected");
Console.WriteLine($"{checks} checks passed; isolated fixtures at {root}");

sealed class FakeClients : IHttpClientFactory
{
    public string LastRequest = "";
    public bool Truncate, MissingTask;
    public HttpClient CreateClient(string name) => new(new Handler(this));
    sealed class Handler(FakeClients owner) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("chat/completions"))
            {
                owner.LastRequest = System.Text.RegularExpressions.Regex.Unescape(await request.Content!.ReadAsStringAsync(cancellationToken));
                var data = "data: " + JsonSerializer.Serialize(new { choices = new[] { new { delta = new { content = "## 测试回复\n**总结**内容。" } } } }) + "\n\n";
                if (!owner.Truncate) data += "data: [DONE]\n\n";
                return new(HttpStatusCode.OK) { Content = new StringContent(data, Encoding.UTF8, "text/event-stream") };
            }
            if (request.Method == HttpMethod.Post)
                return Json("""{"task_id":"task-one","articles":[{"article_id":"queued-one","source_url":"https://mp.weixin.qq.com/s/new-test","status":"pending"}]}""");
            if (owner.MissingTask) return new(HttpStatusCode.NotFound);
            return Json("""{"articles":[{"article_id":"queued-one","status":"processing","progress":55,"stage":"下载图片","logs":[{"time":"2026-10-04T12:00:00","progress":55,"message":"下载图片"}]}]}""");
        }
        static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }
}
sealed class TestEnvironment(string root) : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = root;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "ArticleWorkflow";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = root;
    public string EnvironmentName { get; set; } = "Testing";
}
