using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;

namespace MyKeyVault.Vault.Services;

// Synchronize independently of the browser so completed task metadata survives scraper cleanup.
public sealed class ArticleTaskSyncWorker(IServiceScopeFactory scopes, ILogger<ArticleTaskSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();
                var owners = await db.KnowledgeArticles.Where(x => x.Status == "pending" || x.Status == "processing")
                    .Select(x => x.OwnerId).Distinct().Take(20).ToListAsync(stoppingToken);
                foreach (var owner in owners)
                    await scope.ServiceProvider.GetRequiredService<ArticleScraperService>().SyncActiveAsync(owner, stoppingToken);
                await db.ArticleExtractions.Where(x => x.Status == "processing" && x.CreatedAtUtc < DateTime.UtcNow.AddMinutes(-7))
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "failed").SetProperty(x => x.ErrorMessage, "上次萃取已中断，请重新发送。")
                        .SetProperty(x => x.CompletedAtUtc, DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Article task sync failed ({FailureType})", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
