using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CrmSyncEndpoints
{
    public static void MapCrmSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/crm-sync").WithTags("CrmSync")
            .RequireAuthorization().RequireRateLimiting("api");

        // GET  /api/crm-sync/configs
        group.MapGet("/configs", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var configs = await db.CrmSyncConfigs
                .Where(c => c.TenantId == tc.TenantId)
                .Select(c => new
                {
                    c.Id, c.Provider, c.IsActive, c.InstanceUrl, c.PortalId,
                    c.LastSyncedAt, c.LastSyncStatus, c.TokenExpiresAt
                })
                .ToListAsync();
            return Results.Ok(configs);
        });

        // POST /api/crm-sync/configs  — save / update credentials
        group.MapPost("/configs", async ([FromBody] CrmSyncConfigDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            var existing = await db.CrmSyncConfigs
                .FirstOrDefaultAsync(c => c.TenantId == tc.TenantId && c.Provider == dto.Provider);

            if (existing != null)
            {
                existing.AccessToken = dto.AccessToken;
                existing.RefreshToken = dto.RefreshToken;
                existing.InstanceUrl = dto.InstanceUrl;
                existing.PortalId = dto.PortalId;
                existing.IsActive = dto.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.CrmSyncConfigs.Add(new CrmSyncConfig
                {
                    TenantId = tc.TenantId,
                    Provider = dto.Provider,
                    AccessToken = dto.AccessToken,
                    RefreshToken = dto.RefreshToken,
                    InstanceUrl = dto.InstanceUrl,
                    PortalId = dto.PortalId,
                    IsActive = dto.IsActive
                });
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { message = $"{dto.Provider} config saved." });
        });

        // POST /api/crm-sync/hubspot/push  — push leads to HubSpot
        group.MapPost("/hubspot/push", async (TenantContext tc, ICrmSyncService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            Hangfire.BackgroundJob.Enqueue(() => svc.SyncLeadsToHubSpotAsync(tc.TenantId, default));
            return Results.Accepted(value: new { message = "HubSpot push queued." });
        });

        // POST /api/crm-sync/hubspot/pull  — pull contacts from HubSpot
        group.MapPost("/hubspot/pull", async (TenantContext tc, ICrmSyncService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            Hangfire.BackgroundJob.Enqueue(() => svc.PullContactsFromHubSpotAsync(tc.TenantId, default));
            return Results.Accepted(value: new { message = "HubSpot pull queued." });
        });

        // POST /api/crm-sync/salesforce/push  — push leads to Salesforce
        group.MapPost("/salesforce/push", async (TenantContext tc, ICrmSyncService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            Hangfire.BackgroundJob.Enqueue(() => svc.SyncLeadsToSalesforceAsync(tc.TenantId, default));
            return Results.Accepted(value: new { message = "Salesforce push queued." });
        });

        // GET  /api/crm-sync/logs  — sync history
        group.MapGet("/logs", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? provider, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.CrmSyncLogs
                .Where(l => l.TenantId == tc.TenantId);
            if (!string.IsNullOrWhiteSpace(provider))
                query = query.Where(l => l.Provider == provider);
            var total = await query.CountAsync();
            var logs = await query.OrderByDescending(l => l.SyncedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, logs });
        });
    }
}

public record CrmSyncConfigDto(string Provider, string? AccessToken, string? RefreshToken,
    string? InstanceUrl, string? PortalId, bool IsActive = true);
