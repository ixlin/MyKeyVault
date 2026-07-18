using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MyKeyVault.Vault.Data;
using MyKeyVault.Vault.Models;

namespace MyKeyVault.Vault.Pages.Vault;

public sealed class RequestsModel(VaultDbContext db, UserManager<VaultUser> users) : PageModel
{
    public IReadOnlyList<RequestSummary> Requests { get; private set; } = Array.Empty<RequestSummary>();
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        Requests = await db.ControlledUseRequests.AsNoTracking().Where(x => x.OwnerId == userId && ((x.Status == ControlledUseRequestStatus.Pending && x.ExpiresAtUtc > DateTime.UtcNow) || x.Status == ControlledUseRequestStatus.Approved))
            .Join(db.VaultItems, request => request.VaultItemId, item => item.Id, (request, item) => new { Request = request, item.Title })
            .OrderBy(x => x.Request.ExpiresAtUtc)
            .Select(x => new RequestSummary(x.Request.Id, x.Title, x.Request.RequestedBy, x.Request.RequestedAction, x.Request.Reason, x.Request.ExpiresAtUtc, x.Request.Status))
            .ToListAsync(cancellationToken);
    }
    public async Task<IActionResult> OnPostAsync(Guid id, string decision, CancellationToken cancellationToken)
    {
        var userId = users.GetUserId(User)!;
        var request = await db.ControlledUseRequests.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, cancellationToken);
        if (request is null || (request.Status == ControlledUseRequestStatus.Pending && request.ExpiresAtUtc <= DateTime.UtcNow)) return RedirectToPage();
        request.Status = decision switch { "approve" when request.Status == ControlledUseRequestStatus.Pending => ControlledUseRequestStatus.Approved, "reject" when request.Status == ControlledUseRequestStatus.Pending => ControlledUseRequestStatus.Rejected, "rotation-confirmed" when request.Status == ControlledUseRequestStatus.Approved => ControlledUseRequestStatus.RotationConfirmed, _ => request.Status };
        request.ResolvedAtUtc = DateTime.UtcNow;
        db.SecurityAuditEvents.Add(new SecurityAuditEvent { UserId = userId, VaultItemId = request.VaultItemId, Action = decision == "approve" ? "controlled_use_approved" : decision == "rotation-confirmed" ? "credential_rotation_confirmed" : "controlled_use_rejected", Result = "success", RequestCorrelationId = request.Id.ToString("N") });
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage();
    }
    public sealed record RequestSummary(Guid Id, string ItemTitle, string RequestedBy, string RequestedAction, string? Reason, DateTime ExpiresAtUtc, ControlledUseRequestStatus Status);
}
