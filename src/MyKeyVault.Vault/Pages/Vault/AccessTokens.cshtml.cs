using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;
using MyKeyVault.Vault.Services;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class AccessTokensModel(VaultDbContext db, UserManager<VaultUser> users, McpTokenService tokenService) : PageModel
{
    [BindProperty, Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    public string? NewToken { get; private set; }
    public IReadOnlyList<McpAccessToken> Tokens { get; private set; } = Array.Empty<McpAccessToken>();
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) { await LoadAsync(cancellationToken); return Page(); }
        var token = tokenService.Create(users.GetUserId(User)!, Name, out var rawToken);
        db.McpAccessTokens.Add(token);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = token.OwnerId, Action = "mcp_token_created", Result = "success" });
        await db.SaveChangesAsync(cancellationToken);
        NewToken = rawToken;
        await LoadAsync(cancellationToken);
        return Page();
    }
    public async Task<IActionResult> OnPostRevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = await db.McpAccessTokens.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == users.GetUserId(User) && x.RevokedAtUtc == null, cancellationToken);
        if (token is not null) { token.RevokedAtUtc = DateTime.UtcNow; db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = token.OwnerId, Action = "mcp_token_revoked", Result = "success" }); await db.SaveChangesAsync(cancellationToken); }
        return RedirectToPage();
    }
    private async Task LoadAsync(CancellationToken cancellationToken) => Tokens = await db.McpAccessTokens.AsNoTracking().Where(x => x.OwnerId == users.GetUserId(User) && x.RevokedAtUtc == null).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
}
