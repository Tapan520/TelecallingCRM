using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class QuoteEndpoints
{
    public static void MapQuoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quotes").WithTags("Quotes").RequireAuthorization().RequireRateLimiting("api");

        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? leadId, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = db.Quotes.Where(x => x.TenantId == tc.TenantId);
            if (leadId.HasValue) q = q.Where(x => x.LeadId == leadId.Value);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<QuoteStatus>(status, true, out var qs))
                q = q.Where(x => x.Status == qs);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new {
                    x.Id, x.QuoteNumber, x.Title, x.Status, x.Total, x.Currency,
                    x.ExpiresAt, x.CreatedAt, LeadName = x.Lead.Name, x.LeadId
                }).ToListAsync();
            return Results.Ok(new { total, page, pageSize, items });
        });

        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var q = await db.Quotes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tc.TenantId);
            return q is null ? Results.NotFound() : Results.Ok(q);
        });

        group.MapPost("/", async ([FromBody] QuoteUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var count = await db.Quotes.CountAsync(q => q.TenantId == tc.TenantId);
            var quote = new Quote
            {
                TenantId = tc.TenantId,
                LeadId = dto.LeadId,
                DealId = dto.DealId,
                CreatedById = dto.CreatedById,
                QuoteNumber = $"QT-{DateTime.UtcNow:yyyy}-{(count + 1):D4}",
                Title = dto.Title,
                SubTotal = dto.SubTotal,
                DiscountAmount = dto.DiscountAmount,
                TaxPercent = dto.TaxPercent,
                TaxAmount = Math.Round((dto.SubTotal - dto.DiscountAmount) * dto.TaxPercent / 100, 2),
                Total = Math.Round((dto.SubTotal - dto.DiscountAmount) * (1 + dto.TaxPercent / 100), 2),
                Currency = dto.Currency ?? "INR",
                Notes = dto.Notes,
                LineItemsJson = dto.LineItemsJson ?? "[]",
                ExpiresAt = dto.ExpiresAt,
                Status = QuoteStatus.Draft
            };
            db.Quotes.Add(quote);
            await db.SaveChangesAsync();
            return Results.Created($"/api/quotes/{quote.Id}", new { quote.Id, quote.QuoteNumber, quote.Total });
        });

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] QuoteUpsertDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tc.TenantId);
            if (quote is null) return Results.NotFound();
            quote.Title = dto.Title;
            quote.LeadId = dto.LeadId;
            quote.DealId = dto.DealId;
            quote.SubTotal = dto.SubTotal;
            quote.DiscountAmount = dto.DiscountAmount;
            quote.TaxPercent = dto.TaxPercent;
            quote.TaxAmount = Math.Round((dto.SubTotal - dto.DiscountAmount) * dto.TaxPercent / 100, 2);
            quote.Total = Math.Round((dto.SubTotal - dto.DiscountAmount) * (1 + dto.TaxPercent / 100), 2);
            quote.Currency = dto.Currency ?? "INR";
            quote.Notes = dto.Notes;
            quote.LineItemsJson = dto.LineItemsJson ?? "[]";
            quote.ExpiresAt = dto.ExpiresAt;
            quote.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { quote.Id, quote.QuoteNumber, quote.Total });
        });

        // PATCH /api/quotes/{id}/status
        group.MapPatch("/{id:guid}/status", async (Guid id, [FromBody] QuoteStatusDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tc.TenantId);
            if (quote is null) return Results.NotFound();
            quote.Status = dto.Status;
            if (dto.Status == QuoteStatus.Sent) quote.SentAt = DateTime.UtcNow;
            if (dto.Status == QuoteStatus.Accepted) quote.AcceptedAt = DateTime.UtcNow;
            quote.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { quote.Id, quote.Status });
        });

        // GET /api/quotes/{id}/pdf
        group.MapGet("/{id:guid}/pdf", async (Guid id, TenantContext tc, AppDbContext db, IPdfService pdf) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var quote = await db.Quotes
                .Include(q => q.Lead)
                .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tc.TenantId);
            if (quote == null) return Results.NotFound();
            var tenant = await db.Tenants.FindAsync(tc.TenantId);
            var pdfBytes = pdf.GenerateQuotePdf(quote, tenant?.Name ?? "TelecallingCRM", quote.Lead?.Name ?? "—");
            return Results.File(pdfBytes, "application/pdf", $"{quote.QuoteNumber}.pdf");
        });

        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tc.TenantId);
            if (quote is null) return Results.NotFound();
            db.Quotes.Remove(quote);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record QuoteUpsertDto(
    Guid LeadId, Guid CreatedById, string? Title, decimal SubTotal,
    decimal DiscountAmount, decimal TaxPercent, string? Notes,
    string? LineItemsJson, string? Currency, DateTime? ExpiresAt, Guid? DealId);

public record QuoteStatusDto(QuoteStatus Status);
