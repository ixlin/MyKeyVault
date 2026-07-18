using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyKeyVault.Vault.Data;

/// <summary>
/// Keeps EF migrations independent of developer secrets and any running database.
/// The placeholder is never used by the application at runtime.
/// </summary>
public sealed class VaultDesignTimeDbContextFactory : IDesignTimeDbContextFactory<VaultDbContext>
{
    public VaultDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VaultDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=mykeyvault_next_design;Username=design;Password=design")
            .Options;

        return new VaultDbContext(options);
    }
}
