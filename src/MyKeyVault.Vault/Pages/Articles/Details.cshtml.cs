using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Articles;

[RequestSizeLimit(20000)]
public sealed class DetailsModel(VaultDbContext db, UserManager<VaultUser> users, ArticleScraperService scraper,
    ArticleExtractionService extractionService, ArticleMarkdown markdown) : PageModel
{
    public KnowledgeArticle? Article { get; private set; }
    public IReadOnlyList<ArticleExtraction> Turns { get; private set; } = Array.Empty<ArticleExtraction>();
    public IReadOnlyList<ConversationRow> Conversations { get; private set; } = Array.Empty<ConversationRow>();
    public string? PreviewUrl { get; private set; }
    public string? PdfUrl { get; private set; }
    public bool AiConfigured { get; private set; }
    public string? ModelName { get; private set; }
    [BindProperty, Required, StringLength(4000, MinimumLength = 2)] public string Prompt { get; set; } = "";
    [BindProperty(SupportsGet = true)] public Guid? Conversation { get; set; }
    public string Render(string? value) => markdown.Render(value);
    public async Task<IActionResult> OnGetAsync(long id, long? extraction, CancellationToken cancellationToken)
    {
        if (extraction is not null && Conversation is null)
            Conversation = await db.ArticleExtractions.Where(x => x.Id == extraction && x.ArticleId == id && x.OwnerId == users.GetUserId(User))
                .Select(x => (Guid?)x.ConversationId).SingleOrDefaultAsync(cancellationToken);
        return await LoadAsync(id, cancellationToken) ? Page() : NotFound();
    }
    public async Task<IActionResult> OnGetHistoryAsync(long id, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(id, cancellationToken)) return NotFound();
        return new JsonResult(new { conversations = Conversations, turns = Turns.Select(x => new { x.Id, x.Prompt, x.Status, x.ErrorMessage, x.ModelUsed, x.CreatedAtUtc, html = markdown.Render(x.Result) }) });
    }
    public async Task<IActionResult> OnPostExtractAsync(long id, CancellationToken cancellationToken)
    {
        if (!await db.KnowledgeArticles.AnyAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User), cancellationToken)) return NotFound();
        if (!ModelState.IsValid) return BadRequest(new { error = "请输入 2–4000 个字符的问题。" });
        Response.ContentType = "application/x-ndjson; charset=utf-8";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Accel-Buffering"] = "no";
        await extractionService.RunAsync(id, users.GetUserId(User)!, Conversation, Prompt, async item =>
        {
            await Response.WriteAsync(JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }, cancellationToken);
        return new EmptyResult();
    }
    public async Task<IActionResult> OnGetDownloadAsync(long id, long? extractionId, string? format, CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        // Old per-turn links now export their entire conversation as well.
        if (Conversation is null && extractionId is not null)
            Conversation = await db.ArticleExtractions.Where(x => x.Id == extractionId && x.ArticleId == id && x.OwnerId == ownerId)
                .Select(x => (Guid?)x.ConversationId).SingleOrDefaultAsync(cancellationToken);
        if (Conversation is null) return NotFound();
        var article = await db.KnowledgeArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);
        if (article is null) return NotFound();
        var turns = await db.ArticleExtractions.AsNoTracking().Where(x => x.ArticleId == id && x.OwnerId == ownerId && x.ConversationId == Conversation)
            .OrderBy(x => x.Id).ToListAsync(cancellationToken);
        if (turns.Count == 0) return NotFound();
        if (format is not (null or "markdown" or "html")) return BadRequest();
        var html = format == "html";
        var content = html ? ArticleConversationExport.Html(article, turns, markdown) : ArticleConversationExport.Markdown(article, turns);
        return File(System.Text.Encoding.UTF8.GetBytes(content), html ? "text/html; charset=utf-8" : "text/markdown; charset=utf-8",
            $"article-{id}-conversation-{Conversation.Value:N}.{(html ? "html" : "md")}");
    }
    private async Task<bool> LoadAsync(long id, CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        Article = await db.KnowledgeArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);
        if (Article is null) return false;
        var history = await db.ArticleExtractions.AsNoTracking().Where(x => x.ArticleId == id && x.OwnerId == ownerId)
            .OrderByDescending(x => x.Id).Select(x => new { x.Id, x.ConversationId, x.Prompt, x.CreatedAtUtc, x.Status }).ToListAsync(cancellationToken);
        Conversations = history.GroupBy(x => x.ConversationId).Select(g => new ConversationRow(g.Key, g.Last().Prompt, g.First().CreatedAtUtc, g.Count(), g.First().Status)).ToList();
        if (Conversation is not null && !Conversations.Any(x => x.Id == Conversation)) return false;
        if (Conversation is not null) Turns = await db.ArticleExtractions.AsNoTracking()
            .Where(x => x.ArticleId == id && x.OwnerId == ownerId && x.ConversationId == Conversation).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        PreviewUrl = scraper.GetPreviewUrl(Article); PdfUrl = scraper.GetPreviewUrl(Article, true);
        ModelName = await db.ArticleAiSettings.Where(x => x.OwnerId == ownerId).Select(x => x.ModelName).SingleOrDefaultAsync(cancellationToken);
        AiConfigured = ModelName is not null;
        return true;
    }
    public sealed record ConversationRow(Guid Id, string Title, DateTime UpdatedAtUtc, int Count, string Status);
}
