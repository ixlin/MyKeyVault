using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<VaultAccount> Accounts => Set<VaultAccount>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AccountTag> AccountTags => Set<AccountTag>();
    public DbSet<UserKeys> UserKeys => Set<UserKeys>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();

    // Tushare 数据表
    public DbSet<TushareApp> TushareApps => Set<TushareApp>();
    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<StockBasic> StockBasics => Set<StockBasic>();
    public DbSet<StockDaily> StockDailies => Set<StockDaily>();
    public DbSet<IncomeStatement> IncomeStatements => Set<IncomeStatement>();
    public DbSet<BalanceSheet> BalanceSheets => Set<BalanceSheet>();
    public DbSet<CashflowStatement> CashflowStatements => Set<CashflowStatement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<VaultAccount>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        builder.Entity<Tag>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.TagName }).IsUnique();
        });

        builder.Entity<AccountTag>(e =>
        {
            e.HasIndex(x => new { x.AccountId, x.TagId }).IsUnique();
            e.HasOne(at => at.Account)
                .WithMany(a => a.AccountTags)
                .HasForeignKey(at => at.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(at => at.Tag)
                .WithMany(t => t.AccountTags)
                .HasForeignKey(at => at.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserKeys>(e =>
        {
            e.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<TermsAcceptance>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Version }).IsUnique();
            e.Property(x => x.Version).HasMaxLength(64).IsRequired();
        });

        // Tushare 表配置
        builder.Entity<TushareApp>(e =>
        {
            e.HasIndex(x => x.AppId).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<CallLog>(e =>
        {
            e.HasIndex(x => new { x.AppId, x.RequestAt });
            e.HasIndex(x => x.ApiName);
        });
    }
}
