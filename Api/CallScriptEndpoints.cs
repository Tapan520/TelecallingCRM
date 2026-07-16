using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class CallScriptEndpoints
{
    public static void MapCallScriptEndpoints(this WebApplication app)
    {
        var scripts = app.MapGroup("/api/call-scripts").WithTags("CallScripts")
            .RequireAuthorization().RequireRateLimiting("api");

        var dispositions = app.MapGroup("/api/dispositions").WithTags("CallDispositions")
            .RequireAuthorization().RequireRateLimiting("api");

        // ---------------------------------------------------------------- Scripts

        scripts.MapGet("/", async (TenantContext tc, AppDbContext db, [FromQuery] Guid? campaignId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.CallScripts.Where(s => s.TenantId == tc.TenantId && s.IsActive);
            if (campaignId.HasValue)
                query = query.Where(s => s.CampaignId == campaignId || s.CampaignId == null);
            var scripts = await query.OrderByDescending(s => s.UpdatedAt)
                .Select(s => new { s.Id, s.Title, s.CampaignId, s.IsActive, s.UpdatedAt })
                .ToListAsync();
            return Results.Ok(scripts);
        });

        scripts.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var script = await db.CallScripts
                .Include(s => s.Dispositions.Where(d => d.IsActive).OrderBy(d => d.SortOrder))
                .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            return script == null ? Results.NotFound() : Results.Ok(script);
        });

        scripts.MapPost("/", async ([FromBody] ScriptUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var script = new CallScript
            {
                TenantId = tc.TenantId, CampaignId = dto.CampaignId,
                Title = dto.Title, Content = dto.Content, IsActive = dto.IsActive
            };
            db.CallScripts.Add(script);
            await db.SaveChangesAsync();
            return Results.Created($"/api/call-scripts/{script.Id}", new { script.Id, script.Title });
        });

        scripts.MapPut("/{id:guid}", async (Guid id, [FromBody] ScriptUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var script = await db.CallScripts.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (script == null) return Results.NotFound();
            script.Title = dto.Title; script.Content = dto.Content;
            script.CampaignId = dto.CampaignId; script.IsActive = dto.IsActive;
            script.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { script.Id, script.Title });
        });

        scripts.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var script = await db.CallScripts.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (script == null) return Results.NotFound();
            db.CallScripts.Remove(script);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------------------------------------------------------------- Dispositions

        dispositions.MapGet("/", async (TenantContext tc, AppDbContext db, [FromQuery] Guid? scriptId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.CallDispositions.Where(d => d.TenantId == tc.TenantId);
            if (scriptId.HasValue) query = query.Where(d => d.ScriptId == scriptId);
            var disps = await query.OrderBy(d => d.SortOrder).ToListAsync();
            return Results.Ok(disps);
        });

        dispositions.MapPost("/", async ([FromBody] DispositionUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var disp = new CallDisposition
            {
                TenantId = tc.TenantId, ScriptId = dto.ScriptId,
                Label = dto.Label, Color = dto.Color, ClosesLead = dto.ClosesLead,
                NextStatus = dto.NextStatus, SortOrder = dto.SortOrder, IsActive = dto.IsActive
            };
            db.CallDispositions.Add(disp);
            await db.SaveChangesAsync();
            return Results.Created($"/api/dispositions/{disp.Id}", disp);
        });

        dispositions.MapPut("/{id:guid}", async (Guid id, [FromBody] DispositionUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var disp = await db.CallDispositions.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (disp == null) return Results.NotFound();
            disp.Label = dto.Label; disp.Color = dto.Color; disp.ClosesLead = dto.ClosesLead;
            disp.NextStatus = dto.NextStatus; disp.SortOrder = dto.SortOrder; disp.IsActive = dto.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(disp);
        });

        dispositions.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var disp = await db.CallDispositions.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (disp == null) return Results.NotFound();
            db.CallDispositions.Remove(disp);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record ScriptUpsertDto(string Title, string Content, Guid? CampaignId, bool IsActive = true);
public record DispositionUpsertDto(string Label, string? Color, bool ClosesLead, LeadStatus? NextStatus,
    int SortOrder, bool IsActive, Guid? ScriptId);
