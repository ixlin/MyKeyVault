using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Serilog 基础日志（控制台）
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        // 注册与登录：不强制邮箱确认（后续可启用）
        options.SignIn.RequireConfirmedAccount = false;
        // 密码强度策略（启用）
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars = 5;
        // 用户名允许邮箱或手机号（Identity 自带 Email 与 PhoneNumber 字段）
        options.User.RequireUniqueEmail = false; // 邮箱或手机号二选一，允许邮箱非唯一，登录逻辑后续自定义
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 记录每个 HTTP 请求的简要日志
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// 法律条款强制接受：若未接受“当前版本”则重定向
const string CURRENT_TERMS_VERSION = "v1"; // 升级条款版本只需修改这里
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (context.User?.Identity?.IsAuthenticated == true &&
        !path.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/Legal/Terms", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/Legal/Accept", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/Legal/Policy", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase))
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var accepted = await db.TermsAcceptances.AnyAsync(t => t.UserId == userId && t.Version == CURRENT_TERMS_VERSION);
            if (!accepted)
            {
                context.Response.Redirect("/Legal/Terms");
                return;
            }
        }
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
