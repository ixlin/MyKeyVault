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

    private async Task LoadAsync(string ownerId, CancellationToken cancellationToken)
    {
        ScraperAvailable = await scraper.IsAvailableAsync(cancellationToken);
        AiConfigured = await db.ArticleAiSettings.AnyAsync(x => x.OwnerId == ownerId, cancellationToken);
        Articles = await db.KnowledgeArticles.AsNoTracking().Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200)
            .Select(x => new ArticleRow(x.Id, x.Title, x.Author, x.PublishedText, x.Status, x.ErrorMessage, x.ImagesCount, x.VideosCount, x.CreatedAtUtc, x.Extractions.Count(e => e.Status == "completed")))
            .ToListAsync(cancellationToken);
    }

    public sealed record ArticleRow(long Id, string? Title, string? Author, string? PublishedText, string Status, string? Error, int Images, int Videos, DateTime CreatedAtUtc, int ExtractionCount);
}
