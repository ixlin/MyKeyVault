using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;
using Serilog;

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
builder.Services.AddScoped<McpTokenService>();

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
});

var app = builder.Build();
await BootstrapAccountInitializer.InitializeAsync(app.Services, builder.Configuration);
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/controlled-use-requests", async (HttpRequest request, ControlledUseRequestInput input, McpTokenService tokenService, VaultDbContext db, CancellationToken cancellationToken) =>
{
    var authorization = request.Headers.Authorization.ToString();
    var rawToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..].Trim() : string.Empty;
    var token = await tokenService.ValidateAsync(rawToken, cancellationToken);
    if (token is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(input.RequestedAction) || input.RequestedAction.Length > 120 || input.Reason?.Length > 500) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["动作或说明无效。"] });
    var item = await db.VaultItems.SingleOrDefaultAsync(x => x.Id == input.VaultItemId && x.OwnerId == token.OwnerId && !x.IsArchived, cancellationToken);
    if (item is null) return Results.NotFound();
    var controlledRequest = new ControlledUseRequest { OwnerId = token.OwnerId, VaultItemId = item.Id, RequestedBy = token.Name, RequestedAction = input.RequestedAction.Trim(), Reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim() };
    db.ControlledUseRequests.Add(controlledRequest);
    db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = token.OwnerId, VaultItemId = item.Id, Action = "controlled_use_requested", Result = "pending", RequestCorrelationId = controlledRequest.Id.ToString("N") });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Accepted($"/api/controlled-use-requests/{controlledRequest.Id}", new { requestId = controlledRequest.Id, status = controlledRequest.Status.ToString(), expiresAtUtc = controlledRequest.ExpiresAtUtc });
});
app.MapGet("/api/controlled-use-requests/{id:guid}", async (Guid id, HttpRequest request, McpTokenService tokenService, VaultDbContext db, CancellationToken cancellationToken) =>
{
    var authorization = request.Headers.Authorization.ToString();
    var rawToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..].Trim() : string.Empty;
    var token = await tokenService.ValidateAsync(rawToken, cancellationToken);
    if (token is null) return Results.Unauthorized();
    var controlledRequest = await db.ControlledUseRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == token.OwnerId, cancellationToken);
    if (controlledRequest is null) return Results.NotFound();
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { requestId = controlledRequest.Id, status = controlledRequest.Status.ToString(), expiresAtUtc = controlledRequest.ExpiresAtUtc, resolvedAtUtc = controlledRequest.ResolvedAtUtc });
});
app.MapRazorPages();
app.Run();

public sealed record ControlledUseRequestInput(Guid VaultItemId, string RequestedAction, string? Reason);
