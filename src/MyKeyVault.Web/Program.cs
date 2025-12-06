using Microsoft.AspNetCore.Identity;
using MyKeyVault.Web.Data;
using MyKeyVault.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using MyKeyVault.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Serilog 基础日志（控制台）
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .MinimumLevel.Information() // 减少日志输出，只显示信息级别以上
    .MinimumLevel.Override("MyKeyVault.Web.Services.EmailSender", LogEventLevel.Debug) // 邮件服务显示详细日志
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning) // 减少ASP.NET Core框架日志
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning) // 减少EF Core日志
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
    .AddRoles<IdentityRole>() // 添加角色支持
    .AddEntityFrameworkStores<ApplicationDbContext>();

// 注册邮件发送服务
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, MyKeyVault.Web.Services.EmailSender>();

// 缩短 SecurityStamp 校验间隔，便于角色变更后尽快生效（默认30分钟）
builder.Services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(o =>
{
    o.ValidationInterval = TimeSpan.FromMinutes(1);
});

// Tushare 服务配置
builder.Services.Configure<MyKeyVault.Web.Services.TushareOptions>(
    builder.Configuration.GetSection(MyKeyVault.Web.Services.TushareOptions.SectionName));
builder.Services.AddHttpClient<MyKeyVault.Web.Services.TushareApiService>();
builder.Services.AddScoped<MyKeyVault.Web.Services.TushareAuthService>();
builder.Services.AddScoped<MyKeyVault.Web.Services.TushareDataService>();

// 微信文章爬虫服务配置
builder.Services.Configure<WechatScraperOptions>(
    builder.Configuration.GetSection("WechatScraper"));
builder.Services.AddHttpClient<WechatScraperService>();
builder.Services.AddScoped<WechatScraperService>();

// JWT 认证（用于 Tushare API）
builder.Services.AddAuthentication()
    .AddJwtBearer("TushareBearer", options =>
    {
        var tushareConfig = builder.Configuration.GetSection(MyKeyVault.Web.Services.TushareOptions.SectionName);
        var jwtSecret = tushareConfig.GetValue<string>("JwtSecret") ?? throw new InvalidOperationException("Tushare:JwtSecret not configured");
        
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "MyKeyVault.Tushare",
            ValidAudience = "TushareClient",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Cookie 设置：支持小程序 HTTPS 跨域携带与 API 直接返回 401/403
builder.Services.ConfigureApplicationCookie(options =>
{
    // 开发环境使用 Lax，生产环境使用 None（配合 HTTPS）
    options.Cookie.SameSite = builder.Environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    // 本地调试：不启用 HTTPS 重定向，避免小程序开发者工具/HTTP 直连被 302
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();

// 记录每个 HTTP 请求的简要日志
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        if (httpContext.User?.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserAuthenticated", true);
            diagnosticContext.Set("UserName", httpContext.User?.Identity?.Name);
        }
        else
        {
            diagnosticContext.Set("UserAuthenticated", false);
        }
    };
});

// 为小程序 API 添加专门的调试中间件
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/api/mp", StringComparison.OrdinalIgnoreCase))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        
        // 记录请求开始
        logger.LogInformation("🔍 [MP-API] {Method} {Path} - User: {IsAuth} {UserName}",
            context.Request.Method,
            path,
            context.User?.Identity?.IsAuthenticated ?? false,
            context.User?.Identity?.Name ?? "Anonymous");
        
        // 记录 Cookie 信息
        var hasCookies = context.Request.Cookies.Any();
        var cookieNames = string.Join(", ", context.Request.Cookies.Keys);
        logger.LogInformation("🍪 [MP-API] Cookies: {HasCookies} - Names: [{CookieNames}]",
            hasCookies, cookieNames);
    }
    
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// 法律条款强制接受：
// - 对 API 路径 /api/mp/** 若未接受则返回 451 JSON（不重定向）
// - 对 MVC 页面仍采用重定向到 /Legal/Terms
const string CURRENT_TERMS_VERSION = "v1"; // 升级条款版本只需修改这里
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        logger.LogInformation("📋 [TERMS] Path: {Path} - User: {UserId}", path, userId);
        
        // � 重新启用法律条款检查，增加详细日志
        bool skipTermsCheck = false; // 启用法律条款检查
        
        // API：/api/mp/** 返回 451 JSON（放行 /api/mp/legal/**）
        if (!skipTermsCheck &&
            path.StartsWith("/api/mp", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/api/mp/legal", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(userId))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // 增加详细的查询日志
                logger.LogInformation("📋 [TERMS] Checking terms for User: {UserId}, Version: {Version}", userId, CURRENT_TERMS_VERSION);
                
                var termsRecord = await db.TermsAcceptances
                    .Where(t => t.UserId == userId && t.Version == CURRENT_TERMS_VERSION)
                    .FirstOrDefaultAsync();
                
                bool accepted = termsRecord != null;
                
                if (termsRecord != null)
                {
                    logger.LogInformation("📋 [TERMS] ✅ User {UserId} accepted terms {Version} at {AcceptedAt}", 
                        userId, termsRecord.Version, termsRecord.AcceptedAt);
                }
                else
                {
                    // 查询该用户的所有条款记录
                    var allTerms = await db.TermsAcceptances
                        .Where(t => t.UserId == userId)
                        .Select(t => new { t.Version, t.AcceptedAt })
                        .ToListAsync();
                    
                    logger.LogWarning("📋 [TERMS] ❌ User {UserId} has NOT accepted terms {Version}. Found records: {@AllTerms}", 
                        userId, CURRENT_TERMS_VERSION, allTerms);
                }
                
                logger.LogInformation("📋 [TERMS] User {UserId} terms acceptance: {Accepted}", userId, accepted);
                
                if (!accepted)
                {
                    logger.LogWarning("🚫 [TERMS] Returning 451 for {Path} - User {UserId} has not accepted terms", path, userId);
                    context.Response.StatusCode = 451; // Unavailable For Legal Reasons
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"code\":451,\"message\":\"Terms not accepted\"}");
                    return;
                }
            }
        }
        // MVC 页面：仍做重定向，但放行静态资源与法律页面
        else if (!path.StartsWith("/Identity", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/Legal/Terms", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/Legal/Accept", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/Legal/Policy", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) &&
                 !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(userId))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var accepted = await db.TermsAcceptances.AnyAsync(t => t.UserId == userId && t.Version == CURRENT_TERMS_VERSION);
                if (!accepted)
                {
                    logger.LogWarning("🚫 [TERMS] Redirecting to /Legal/Terms for {Path} - User {UserId} has not accepted terms", path, userId);
                    context.Response.Redirect("/Legal/Terms");
                    return;
                }
            }
        }
    }
    else
    {
        if (path.StartsWith("/api/mp", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("🔐 [AUTH] Unauthenticated request to {Path}", path);
        }
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// 在生产环境自动应用数据库迁移
if (!app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Log.Information("🗄️ Applying database migrations...");
            await db.Database.MigrateAsync();
            Log.Information("✅ Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Error applying database migrations");
            throw;
        }
    }
}

// 初始化管理员
using (var scope = app.Services.CreateScope())
{
    await AdminInitializer.InitializeAsync(scope.ServiceProvider);
}

app.Run();
