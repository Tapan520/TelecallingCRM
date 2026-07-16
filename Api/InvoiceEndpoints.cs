using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
using System.Security.Claims;

namespace TelecallingCRM.Api;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invoices").WithTags("Invoices")
            .RequireAuthorization().RequireRateLimiting("api");

        // GET /api/invoices
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? leadId, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.Invoices.Where(i => i.TenantId == tc.TenantId);
            if (leadId.HasValue) query = query.Where(i => i.LeadId == leadId);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InvoiceStatus>(status, true, out var s))
                query = query.Where(i => i.Status == s);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(i => i.IssuedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(i => new
                {
                    i.Id, i.InvoiceNumber, i.LeadId,
                    LeadName = i.Lead.Name,
                    i.Status, i.Total, i.Currency, i.IssuedAt, i.DueAt, i.PaidAt
                })
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET /api/invoices/{id}
        group.MapGet("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var inv = await db.Invoices
                .Include(i => i.Lead)
                .Include(i => i.CreatedBy)
                .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tc.TenantId);
            return inv == null ? Results.NotFound() : Results.Ok(inv);
        });

        // POST /api/invoices
        group.MapPost("/", async ([FromBody] CreateInvoiceDto dto, TenantContext tc,
            IInvoiceService svc, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var lineItems = dto.LineItems.Select(li => new InvoiceLineItem(li.Description, li.Qty, li.UnitPrice)).ToList();
            var inv = await svc.CreateInvoiceAsync(tc.TenantId, dto.LeadId, userId,
                lineItems, dto.TaxPercent, dto.Description, dto.Notes, dto.DueAt, dto.PaymentId);
            return Results.Created($"/api/invoices/{inv.Id}", new { inv.Id, inv.InvoiceNumber, inv.Total });
        });

        // POST /api/invoices/{id}/send  — mark as Sent
        group.MapPost("/{id:guid}/send", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tc.TenantId);
            if (inv == null) return Results.NotFound();
            if (inv.Status == InvoiceStatus.Draft) { inv.Status = InvoiceStatus.Sent; inv.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
            return Results.Ok(new { inv.Status });
        });

        // POST /api/invoices/{id}/paid
        group.MapPost("/{id:guid}/paid", async (Guid id, TenantContext tc, IInvoiceService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            await svc.MarkPaidAsync(id, tc.TenantId);
            return Results.Ok(new { message = "Invoice marked paid." });
        });

        // POST /api/invoices/{id}/void
        group.MapPost("/{id:guid}/void", async (Guid id, TenantContext tc, IInvoiceService svc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            await svc.VoidInvoiceAsync(id, tc.TenantId);
            return Results.Ok(new { message = "Invoice voided." });
        });

        // DELETE /api/invoices/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var inv = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.TenantId == tc.TenantId);
            if (inv == null) return Results.NotFound();
            db.Invoices.Remove(inv);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

public record CreateInvoiceDto(Guid LeadId, Guid? PaymentId, List<LineItemDto> LineItems,
    decimal TaxPercent, string? Description, string? Notes, DateTime? DueAt);
public record LineItemDto(string Description, int Qty, decimal UnitPrice);
