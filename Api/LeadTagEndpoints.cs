using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class LeadTagEndpoints
{
    public static void MapLeadTagEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tags")
            .WithTags("Tags")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tags = await db.LeadTags
                .Where(t => t.TenantId == tc.TenantId)
                .OrderBy(t => t.Name)
                .Select(t => new {
                    t.Id, t.Name, t.Color, t.CreatedAt,
                    UsageCount = db.Leads.Count(l => l.TenantId == tc.TenantId
                        && l.Tags != null && l.Tags.Contains(t.Name))
                })
                .ToListAsync();
            return Results.Ok(tags);
        });

        group.MapPost("/", async ([FromBody] TagUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var exists = await db.LeadTags.AnyAsync(t => t.TenantId == tc.TenantId
                && t.Name.ToLower() == dto.Name.ToLower());
            if (exists) return Results.Conflict("Tag already exists.");
            var tag = new LeadTag {
                TenantId = tc.TenantId,
                Name = dto.Name.Trim(),
                Color = dto.Color
            };
            db.LeadTags.Add(tag);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tags/{tag.Id}", new { tag.Id, tag.Name, tag.Color });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] TagUpsertDto dto,
            TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tag = await db.LeadTags.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (tag == null) return Results.NotFound();
            var oldName = tag.Name;
            tag.Name = dto.Name.Trim();
            tag.Color = dto.Color;
            // Update all leads that have the old tag name
            if (oldName != tag.Name)
            {
                var affectedLeads = await db.Leads
                    .Where(l => l.TenantId == tc.TenantId && l.Tags != null && l.Tags.Contains(oldName))
                    .ToListAsync();
                foreach (var lead in affectedLeads)
                    lead.Tags = lead.Tags!.Replace(oldName, tag.Name);
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { tag.Id, tag.Name, tag.Color });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"));

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var tag = await db.LeadTags.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tc.TenantId);
            if (tag == null) return Results.NotFound();
            db.LeadTags.Remove(tag);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("admin", "manager", "superadmin"));
    }
}

public record TagUpsertDto(string Name, string Color);
