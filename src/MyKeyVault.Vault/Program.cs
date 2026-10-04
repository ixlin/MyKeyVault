using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;
using Serilog;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, _, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

builder.Services.AddDbContext<VaultDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.Configure<VaultEncryptionOptions>(builder.Configuration.GetSection(VaultEncryptionOptions.SectionName));
builder.Services.AddSingleton<SecretCipher>();
builder.Services.Configure<ArticleScraperOptions>(builder.Configuration.GetSection(ArticleScraperOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddScoped<ArticleScraperService>();
if (!string.Equals(Environment.GetEnvironmentVariable("MYKEYVAULT_EF_DESIGN"), "1", StringComparison.Ordinal))
    builder.Services.AddHostedService<ArticleTaskSyncWorker>();
builder.Services.AddScoped<ArticleExtractionService>();
builder.Services.AddHttpClient(nameof(ArticleExtractionService), client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(PublicAiConnection.CreateHandler);
builder.Services.AddSingleton<ArticleMarkdown>();
builder.Services.Configure<ResetEmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("password-recovery", context => context.Request.Method != "POST"
        ? RateLimitPartition.GetNoLimiter("read")
        : RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        context.HttpContext.Response.Headers.RetryAfter = "600";
        await context.HttpContext.Response.WriteAsync("操作过于频繁，请 10 分钟后重试。", cancellationToken);
    };
});

builder.Services.AddDefaultIdentity<VaultUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<VaultDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-MyKeyVault";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Vault");
    options.Conventions.AuthorizeFolder("/Articles");
});

var app = builder.Build();
var isEfDesignTime = string.Equals(Environment.GetEnvironmentVariable("MYKEYVAULT_EF_DESIGN"), "1", StringComparison.Ordinal);
if (!isEfDesignTime)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VaultDbContext>();
    await db.Database.MigrateAsync();
    await BootstrapAccountInitializer.InitializeAsync(app.Services, builder.Configuration);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/Vault") || context.Request.Path.StartsWithSegments("/Articles") || context.Request.Path.StartsWithSegments("/Identity/Account"))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "private, no-store, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            return Task.CompletedTask;
        });
    }
    await next();
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/wechat-articles", out var articlePath) &&
        (context.User.Identity?.IsAuthenticated != true ||
         articlePath.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() !=
         context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/wechat-articles"))
        {
            // Keep article scripts disabled, but retain the site's origin so authenticated
            // image/media subrequests carry the Identity cookie instead of being rejected.
            context.Context.Response.Headers.ContentSecurityPolicy = "sandbox allow-same-origin; default-src 'none'; img-src 'self' data:; media-src 'self' data:; style-src 'unsafe-inline'";
            context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Context.Response.Headers.CacheControl = "private, no-store";
        }
    }
});
app.UseAuthorization();

app.MapRazorPages();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
