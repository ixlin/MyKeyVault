using Microsoft.AspNetCore.Identity;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Services;

/// <summary>
/// Creates explicitly configured operational accounts during a controlled deployment.
/// No account is created unless every required bootstrap setting is supplied.
/// </summary>
public static class BootstrapAccountInitializer
{
    private const string AdminRole = "Admin";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var adminEmail = configuration["BootstrapAccounts:AdminEmail"];
        var adminPassword = configuration["BootstrapAccounts:AdminPassword"];
        var testEmail = configuration["BootstrapAccounts:TestEmail"];
        var testPassword = configuration["BootstrapAccounts:TestPassword"];

        if (new[] { adminEmail, adminPassword, testEmail, testPassword }.Any(string.IsNullOrWhiteSpace)) return;

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<VaultUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdminRole));
            if (!roleResult.Succeeded) throw new InvalidOperationException("无法初始化管理员角色。");
        }

        var admin = await EnsureUserAsync(userManager, adminEmail!, adminPassword!);
        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, AdminRole);
            if (!roleResult.Succeeded) throw new InvalidOperationException("无法授予管理员角色。");
        }

        await EnsureUserAsync(userManager, testEmail!, testPassword!);
    }

    private static async Task<VaultUser> EnsureUserAsync(UserManager<VaultUser> userManager, string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
        {
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded) throw new InvalidOperationException($"无法确认账号 {email}。");
            }
            return user;
        }

        user = new VaultUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded) throw new InvalidOperationException($"无法创建账号 {email}。");
        return user;
    }
}
