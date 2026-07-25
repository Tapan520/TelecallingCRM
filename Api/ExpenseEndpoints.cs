using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/expenses")
            .WithTags("Expenses")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET expenses
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var query = db.Expenses.Where(e => e.TenantId == tc.TenantId).AsQueryable();

            if (role != "admin" && role != "manager")
                query = query.Where(e => e.AgentId == callerId);
            else if (agentId.HasValue)
                query = query.Where(e => e.AgentId == agentId.Value);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ExpenseStatus>(status, out var es))
                query = query.Where(e => e.Status == es);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(e => new {
                    e.Id, e.AgentId, AgentName = e.Agent.FullName,
                    e.Category, e.Description, e.Amount, e.ExpenseDate,
                    e.Status, e.ReviewerNotes, e.ReviewedAt,
                    ReviewedBy = e.ReviewedBy != null ? e.ReviewedBy.FullName : null,
                    e.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // POST submit expense
        group.MapPost("/", async ([FromBody] SubmitExpenseDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var expense = new Expense
            {
                TenantId = tc.TenantId, AgentId = callerId,
                Category = dto.Category, Description = dto.Description,
                Amount = dto.Amount, ExpenseDate = dto.ExpenseDate.Date
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            return Results.Created($"/api/expenses/{expense.Id}", new { expense.Id });
        });

        // PUT review (admin/manager)
        group.MapPut("/{id:guid}/review", async (Guid id, [FromBody] ReviewExpenseDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (expense == null) return Results.NotFound();
            if (expense.Status != ExpenseStatus.Pending) return Results.BadRequest("Already reviewed.");

            expense.Status = dto.Approve ? ExpenseStatus.Approved : ExpenseStatus.Rejected;
            expense.ReviewedById = callerId;
            expense.ReviewedAt = DateTime.UtcNow;
            expense.ReviewerNotes = dto.Notes;
            expense.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { expense.Id, expense.Status });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // DELETE cancel (own pending only)
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.TenantId == tc.TenantId);
            if (expense == null) return Results.NotFound();
            if (expense.AgentId != callerId) return Results.Forbid();
            if (expense.Status != ExpenseStatus.Pending) return Results.BadRequest("Cannot delete reviewed expenses.");
            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }
}

public record SubmitExpenseDto(ExpenseCategory Category, string Description, decimal Amount, DateTime ExpenseDate);
public record ReviewExpenseDto(bool Approve, string? Notes);
