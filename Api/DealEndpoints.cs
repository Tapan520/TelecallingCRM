using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

public static class DealEndpoints
{
    public static void MapDealEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/deals").WithTags("Deals").RequireAuthorization().RequireRateLimiting("api");

        // GET /api/deals
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] string? stage = null,
            [FromQuery] Guid? leadId = null,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.Deals.Where(d => d.TenantId == tc.TenantId);
            if (!string.IsNullOrWhiteSpace(stage) && Enum.TryParse<DealStage>(stage, true, out var s))
                q = q.Where(d => d.Stage == s);
            if (leadId.HasValue) q = q.Where(d => d.LeadId == leadId.Value);
            var total = await q.CountAsync();
            var deals = await q
                .OrderByDescending(d => d.UpdatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(d => new {
                    d.Id, d.Title, d.Value, d.Currency, d.Stage, d.Probability,
                    d.ExpectedCloseDate, d.CreatedAt, d.UpdatedAt,
                    LeadName = d.Lead.Name, d.LeadId,
                    AssignedTo = d.AssignedTo != null ? d.AssignedTo.FullName : null
                })
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, deals });
        });

        // GET /api/deals/{id}
        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var d = await db.Deals
                .AsNoTracking()
                .Where(x => x.Id == id && x.TenantId == tc.TenantId)
                .Select(x => new {
                    x.Id, x.Title, x.Value, x.Currency, x.Stage, x.Probability,
                    x.ExpectedCloseDate, x.Notes, x.CreatedAt, x.UpdatedAt,
                    LeadName = x.Lead.Name, x.LeadId, x.AssignedToId,
                    AssignedTo = x.AssignedTo != null ? x.AssignedTo.FullName : null
                })
                .FirstOrDefaultAsync();
            return d is null ? Results.NotFound() : Results.Ok(d);
        });

        // POST /api/deals
        group.MapPost("/", async ([FromBody] DealUpsertDto dto, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var deal = new Deal
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                AssignedToId = dto.AssignedToId,
                Title = dto.Title,
                Value = dto.Value,
                Currency = dto.Currency ?? "INR",
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Notes = dto.Notes
            };
            db.Deals.Add(deal);
            await db.SaveChangesAsync();
            return Results.Created($"/api/deals/{deal.Id}", new { deal.Id, deal.Title, deal.Stage, deal.Value });
        });

        // PUT /api/deals/{id}
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] DealUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (deal is null) return Results.NotFound();
            deal.Title = dto.Title;
            deal.LeadId = dto.LeadId;
            deal.AssignedToId = dto.AssignedToId;
            deal.Value = dto.Value;
            deal.Currency = dto.Currency ?? "INR";
            deal.Stage = dto.Stage;
            deal.Probability = dto.Probability;
            deal.ExpectedCloseDate = dto.ExpectedCloseDate;
            deal.Notes = dto.Notes;
            deal.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { deal.Id, deal.Title, deal.Stage, deal.Value, deal.UpdatedAt });
        });

        // DELETE /api/deals/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var deal = await db.Deals.FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tc.TenantId);
            if (deal is null) return Results.NotFound();
            db.Deals.Remove(deal);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GET /api/deals/pipeline-summary — stage-wise totals
        group.MapGet("/pipeline-summary", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var summary = await db.Deals
                .Where(d => d.TenantId == tc.TenantId)
                .GroupBy(d => d.Stage)
                .Select(g => new {
                    Stage = g.Key.ToString(),
                    Count = g.Count(),
                    TotalValue = g.Sum(d => d.Value),
                    WeightedValue = g.Sum(d => d.Value * d.Probability / 100m)
                })
                .ToListAsync();
            return Results.Ok(summary);
        });
    }
}

public record DealUpsertDto(
    string Title, Guid LeadId, decimal Value, DealStage Stage,
    int Probability, string? Notes, string? Currency,
    DateTime? ExpectedCloseDate, Guid? AssignedToId);
