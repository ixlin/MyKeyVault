using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Models;

namespace MyKeyVault.Web.Services;

/// <summary>
/// 初始化管理员角色和用户
/// </summary>
public static class AdminInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // 创建管理员角色
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            logger.LogInformation("已创建管理员角色");
        }

        // 设置指定邮箱为管理员
        var adminEmail = "sfrost@qq.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        
        if (adminUser != null)
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var result = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (result.Succeeded)
                {
                    logger.LogInformation("✓ 已将用户 {Email} 设置为管理员", adminEmail);
                }
                else
                {
                    logger.LogError("✗ 设置管理员失败: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("✓ 用户 {Email} 已经是管理员", adminEmail);
            }
        }
        else
        {
            logger.LogWarning("✗ 未找到邮箱为 {Email} 的用户，请先注册该账号", adminEmail);
        }

        // 创建测试账号（供 Coder熊 测试用）
        var testEmail = "coder-test@mykeyvault.test";
        var testUser = await userManager.FindByEmailAsync(testEmail);
        if (testUser == null)
        {
            testUser = new ApplicationUser
            {
                UserName = testEmail,
                Email = testEmail,
                EmailConfirmed = true,
                TermsAcceptedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(testUser, "CoderTest@2025!");
            if (result.Succeeded)
            {
                logger.LogInformation("✓ 已创建测试账号: {Email} / CoderTest@2025!", testEmail);
                await userManager.AddToRoleAsync(testUser, "Admin");
                
                // 接受服务条款
                var dbContext = serviceProvider.GetRequiredService<Data.ApplicationDbContext>();
                dbContext.TermsAcceptances.Add(new Models.TermsAcceptance { UserId = testUser.Id, Version = "v1" });
                await dbContext.SaveChangesAsync();
                logger.LogInformation("✓ 已接受服务条款");
            }
            else
            {
                logger.LogError("✗ 创建测试账号失败: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogInformation("✓ 测试账号已存在: {Email}", testEmail);
        }
    }
}
