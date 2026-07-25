using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Data;

/// <summary>
/// EF Core SaveChanges interceptor that automatically writes an AuditLog entry
/// for every Insert, Update or Delete on auditable entities.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    // Entity types we don't want to audit (high-volume, noisy, or self-referential)
    private static readonly HashSet<Type> _excluded =
    [
        typeof(UserActivityLog),
        typeof(ActivityLog),
        typeof(Notification),
        typeof(AttendanceLog),
        typeof(AgentPresence),
        typeof(WebhookDeliveryLog),
        typeof(CrmSyncLog),
        typeof(AnnouncementRead),
    ];

    // Auditable entity types ? friendly names
    private static readonly Dictionary<Type, string> _entityNames = new()
    {
        [typeof(Lead)]               = "Lead",
        [typeof(Call)]               = "Call",
        [typeof(Campaign)]           = "Campaign",
        [typeof(Deal)]               = "Deal",
        [typeof(FollowUp)]           = "FollowUp",
        [typeof(TaskItem)]           = "Task",
        [typeof(Payment)]            = "Payment",
        [typeof(Invoice)]            = "Invoice",
        [typeof(Quote)]              = "Quote",
        [typeof(Meeting)]            = "Meeting",
        [typeof(Escalation)]         = "Escalation",
        [typeof(LeaveRequest)]       = "LeaveRequest",
        [typeof(Expense)]            = "Expense",
        [typeof(Announcement)]       = "Announcement",
        [typeof(Holiday)]            = "Holiday",
        [typeof(ShiftSwapRequest)]   = "ShiftSwap",
        [typeof(AgentGoal)]          = "AgentGoal",
        [typeof(CommissionEntry)]    = "Commission",
        [typeof(DncEntry)]           = "DncEntry",
        [typeof(AppUser)]            = "User",
        [typeof(Tenant)]             = "Tenant",
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        WriteAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditEntries(DbContext? context)
    {
        if (context is not AppDbContext db) return;

        var http    = _httpContextAccessor.HttpContext;
        var userId  = GetUserId(http);
        var tenantId = GetTenantId(http);
        var ip      = http?.Connection?.RemoteIpAddress?.ToString();

        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                     && !_excluded.Contains(e.Entity.GetType()))
            .ToList();

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType();
            if (!_entityNames.TryGetValue(entityType, out var name)) continue;

            // Resolve TenantId from the entity itself if available
            var entityTenantId = tenantId;
            var tenantProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
            if (tenantProp?.CurrentValue is Guid tid && tid != Guid.Empty)
                entityTenantId = tid;

            if (entityTenantId == Guid.Empty) continue; // skip system-level non-tenant rows

            var action = entry.State switch
            {
                EntityState.Added    => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted  => "Deleted",
                _                    => "Unknown"
            };

            // Build a concise changed-fields summary for Updates
            var detail = string.Empty;
            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified
                             && !string.Equals(p.OriginalValue?.ToString(), p.CurrentValue?.ToString(), StringComparison.Ordinal))
                    .Select(p => p.Metadata.Name)
                    .ToList();
                if (changed.Count > 0)
                    detail = string.Join(", ", changed);
            }

            // Get entity primary key for reference
            var keyValue = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "";

            db.UserActivityLogs.Add(new UserActivityLog
            {
                TenantId    = entityTenantId,
                UserId      = userId,
                Action      = $"{name}{action}",
                Module      = name,
                Description = string.IsNullOrEmpty(detail)
                    ? $"{name} {action.ToLower()} (id: {keyValue})"
                    : $"{name} {action.ToLower()} — changed: {detail} (id: {keyValue})",
                EntityId    = Guid.TryParse(keyValue, out var eid) ? eid : null,
                IpAddress   = ip,
                CreatedAt   = DateTime.UtcNow
            });
        }
    }

    private static Guid GetUserId(HttpContext? http)
    {
        if (http == null) return Guid.Empty;
        var claim = http.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static Guid GetTenantId(HttpContext? http)
    {
        if (http == null) return Guid.Empty;
        var tenantCtx = http.RequestServices.GetService<Services.TenantContext>();
        return tenantCtx?.TenantId ?? Guid.Empty;
    }
}
