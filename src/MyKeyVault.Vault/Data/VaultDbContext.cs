using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Data;

public sealed class VaultDbContext(DbContextOptions<VaultDbContext> options) : IdentityDbContext<VaultUser>(options)
{
    public DbSet<VaultItem> VaultItems => Set<VaultItem>();
    public DbSet<VaultSecret> VaultSecrets => Set<VaultSecret>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<ControlledUseRequest> ControlledUseRequests => Set<ControlledUseRequest>();
    public DbSet<McpAccessToken> McpAccessTokens => Set<McpAccessToken>();
    public DbSet<VaultTag> VaultTags => Set<VaultTag>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<ArticleExtraction> ArticleExtractions => Set<ArticleExtraction>();
    public DbSet<ArticleAiSettings> ArticleAiSettings => Set<ArticleAiSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<VaultItem>(entity =>
        {
            entity.HasIndex(x => new { x.OwnerId, x.UpdatedAtUtc });
            entity.HasIndex(x => new { x.OwnerId, x.Title });
            entity.HasMany(x => x.Secrets).WithOne(x => x.VaultItem).HasForeignKey(x => x.VaultItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Tags).WithMany(x => x.VaultItems).UsingEntity(join => join.ToTable("VaultItemTags"));
        });
        builder.Entity<VaultSecret>(entity => entity.HasIndex(x => new { x.VaultItemId, x.FieldName }).IsUnique());
        builder.Entity<SecurityAuditEvent>(entity => entity.HasIndex(x => new { x.UserId, x.OccurredAtUtc }));
        builder.Entity<ControlledUseRequest>(entity =>
        {
            entity.HasIndex(x => new { x.OwnerId, x.Status, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.VaultItemId, x.Status });
        });
        builder.Entity<McpAccessToken>(entity =>
        {
            entity.HasIndex(x => x.Prefix);
            entity.HasIndex(x => new { x.OwnerId, x.RevokedAtUtc });
        });
        builder.Entity<VaultTag>(entity => entity.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique());
        builder.Entity<KnowledgeArticle>(entity =>
        {
            entity.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.OwnerId, x.SourceUrl });
            entity.HasMany(x => x.Extractions).WithOne(x => x.Article).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ArticleExtraction>(entity => entity.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc }));
        builder.Entity<ArticleAiSettings>(entity => entity.HasIndex(x => x.OwnerId).IsUnique());
    }
}
