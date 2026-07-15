using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/webhooks").WithTags("Webhooks").RequireAuthorization(p => p.RequireRole("admin", "superadmin")).RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            return Results.Ok(await db.WebhookConfigs
                .Where(w => w.TenantId == tc.TenantId)
                .Select(w => new { w.Id, w.Name, w.Url, w.Events, w.IsActive, w.LastTriggeredAt, w.FailureCount })
                .ToListAsync());
        });

        group.MapPost("/", async ([FromBody] WebhookUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = new WebhookConfig {
                TenantId = tc.TenantId, Name = dto.Name,
                Url = dto.Url, Secret = dto.Secret,
                Events = System.Text.Json.JsonSerializer.Serialize(dto.Events)
            };
            db.WebhookConfigs.Add(hook);
            await db.SaveChangesAsync();
            return Results.Created($"/api/webhooks/{hook.Id}", new { hook.Id, hook.Name, hook.Url });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] WebhookUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = await db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tc.TenantId);
            if (hook == null) return Results.NotFound();
            hook.Name = dto.Name; hook.Url = dto.Url; hook.Secret = dto.Secret;
            hook.Events = System.Text.Json.JsonSerializer.Serialize(dto.Events);
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        group.MapPost("/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = await db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tc.TenantId);
            if (hook == null) return Results.NotFound();
            hook.IsActive = !hook.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { hook.IsActive });
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = await db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tc.TenantId);
            if (hook == null) return Results.NotFound();
            db.WebhookConfigs.Remove(hook);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GET delivery logs for a webhook
        group.MapGet("/{id:guid}/logs", async (Guid id, TenantContext tc, AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = await db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tc.TenantId);
            if (hook == null) return Results.NotFound();

            var total = await db.WebhookDeliveryLogs.CountAsync(l => l.WebhookId == id);
            var logs = await db.WebhookDeliveryLogs
                .Where(l => l.WebhookId == id)
                .OrderByDescending(l => l.AttemptedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new { l.Id, l.EventName, l.HttpStatus, l.Success, l.ErrorMessage, l.AttemptedAt })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, logs });
        });

        // POST manual retry of last failed delivery
        group.MapPost("/{id:guid}/retry", async (Guid id, TenantContext tc, AppDbContext db, IWebhookDispatcher dispatcher) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var hook = await db.WebhookConfigs.FirstOrDefaultAsync(w => w.Id == id && w.TenantId == tc.TenantId);
            if (hook == null) return Results.NotFound();

            var lastFailed = await db.WebhookDeliveryLogs
                .Where(l => l.WebhookId == id && !l.Success)
                .OrderByDescending(l => l.AttemptedAt)
                .FirstOrDefaultAsync();
            if (lastFailed == null) return Results.BadRequest("No failed deliveries to retry.");

            Hangfire.BackgroundJob.Enqueue<IWebhookDispatcher>(d =>
                d.DeliverAsync(id, lastFailed.EventName, new { retried = true }));

            return Results.Accepted();
        });
    }
}

public record WebhookUpsertDto(string Name, string Url, string? Secret, List<string> Events);
