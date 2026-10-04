using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Articles;

public sealed class IndexModel(VaultDbContext db, UserManager<VaultUser> users, ArticleScraperService scraper) : PageModel
{
    [BindProperty, Required, MaxLength(6000)] public string Urls { get; set; } = string.Empty;
    [TempData] public string? Notice { get; set; }
    [BindProperty(SupportsGet = true), MaxLength(100)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (TotalCount + 29) / 30);
    public bool ScraperAvailable { get; private set; }
    public bool AiConfigured { get; private set; }
    public IReadOnlyList<ArticleRow> Articles { get; private set; } = Array.Empty<ArticleRow>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        await scraper.SyncActiveAsync(ownerId, cancellationToken);
        await LoadAsync(ownerId, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        if (!ModelState.IsValid) { await LoadAsync(ownerId, cancellationToken); return Page(); }
        var urls = Urls.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var result = await scraper.SubmitAsync(ownerId, urls, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(Urls), result.Error!);
            await LoadAsync(ownerId, cancellationToken);
            return Page();
        }
        Notice = "抓取任务已提交。文章完成后可直接阅读和萃取。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetProgressAsync(CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        await scraper.SyncActiveAsync(ownerId, cancellationToken);
        await LoadAsync(ownerId, cancellationToken, checkHealth: false);
        return new JsonResult(new { articles = Articles });
    }

    public async Task<IActionResult> OnPostRetryAsync(long id, CancellationToken cancellationToken)
    {
        var result = await scraper.RetryAsync(id, users.GetUserId(User)!, cancellationToken);
        Notice = result.Success ? "已重新提交抓取，请查看最新任务。" : result.Error;
        return RedirectToPage();
    }

    private async Task LoadAsync(string ownerId, CancellationToken cancellationToken, bool checkHealth = true)
    {
        ScraperAvailable = !checkHealth || await scraper.IsAvailableAsync(cancellationToken);
        AiConfigured = await db.ArticleAiSettings.AnyAsync(x => x.OwnerId == ownerId, cancellationToken);
        var query = db.KnowledgeArticles.AsNoTracking().Where(x => x.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(Search)) { var search = Search.Trim()[..Math.Min(100, Search.Trim().Length)]; query = query.Where(x => (x.Title != null && x.Title.Contains(search)) || (x.Author != null && x.Author.Contains(search))); }
        TotalCount = await query.CountAsync(cancellationToken);
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((PageNumber - 1) * 30).Take(30)
            .Select(x => new { x.Id, x.Title, x.Author, x.PublishedText, x.Status, x.ErrorMessage, x.ImagesCount, x.VideosCount, x.CreatedAtUtc,
                Count = x.Extractions.Count(e => e.Status == "completed"), x.Progress, x.Stage, x.ProcessLogJson }).ToListAsync(cancellationToken);
        Articles = rows.Select(x => new ArticleRow(x.Id, x.Title, x.Author, x.PublishedText, x.Status, x.ErrorMessage, x.ImagesCount, x.VideosCount,
            x.CreatedAtUtc, x.Count, x.Progress, x.Stage, ArticleScraperService.ReadLog(x.ProcessLogJson))).ToList();
    }

    public sealed record ArticleRow(long Id, string? Title, string? Author, string? PublishedText, string Status, string? Error, int Images, int Videos, DateTime CreatedAtUtc, int ExtractionCount,
        int Progress, string Stage, IReadOnlyList<ArticleScraperService.ProgressEntry> Logs);
}
