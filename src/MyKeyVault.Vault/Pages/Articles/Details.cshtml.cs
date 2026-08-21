using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Articles;

public sealed class DetailsModel(VaultDbContext db, UserManager<VaultUser> users, ArticleScraperService scraper, ArticleExtractionService extractionService) : PageModel
{
    public KnowledgeArticle? Article { get; private set; }
    public IReadOnlyList<ArticleExtraction> Extractions { get; private set; } = Array.Empty<ArticleExtraction>();
    public string? PreviewUrl { get; private set; }
    public string? PdfUrl { get; private set; }
    public bool AiConfigured { get; private set; }
    [BindProperty, Required, MaxLength(4000)] public string Prompt { get; set; } = string.Empty;
    [TempData] public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken cancellationToken) => await LoadAsync(id, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostExtractAsync(long id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { await LoadAsync(id, cancellationToken); return Page(); }
        var result = await extractionService.ExtractAsync(id, users.GetUserId(User)!, Prompt, cancellationToken);
        Notice = result.Success ? "萃取完成。" : result.Error;
        return RedirectToPage(new { id, extraction = result.ExtractionId });
    }

    private async Task<bool> LoadAsync(long id, CancellationToken cancellationToken)
    {
        var ownerId = users.GetUserId(User)!;
        Article = await db.KnowledgeArticles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, cancellationToken);
        if (Article is null) return false;
        Extractions = await db.ArticleExtractions.AsNoTracking().Where(x => x.ArticleId == id && x.OwnerId == ownerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        PreviewUrl = scraper.GetPreviewUrl(Article);
        PdfUrl = scraper.GetPreviewUrl(Article, true);
        AiConfigured = await db.ArticleAiSettings.AnyAsync(x => x.OwnerId == ownerId, cancellationToken);
        return true;
    }
}
