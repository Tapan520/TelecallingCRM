using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ShiftSwapEndpoints
{
    public static void MapShiftSwapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/shift-swap")
            .WithTags("Shift Swap")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET shift swap requests
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var query = db.ShiftSwapRequests.Where(s => s.TenantId == tc.TenantId).AsQueryable();
            if (role != "admin" && role != "manager")
                query = query.Where(s => s.RequestedById == callerId || s.SwapWithAgentId == callerId);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ShiftSwapStatus>(status, out var ss))
                query = query.Where(s => s.Status == ss);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new {
                    s.Id, s.SwapDate, s.Reason, s.Status, s.ReviewerNotes, s.ReviewedAt,
                    RequestedBy   = s.RequestedBy.FullName,
                    SwapWithAgent = s.SwapWithAgent != null ? s.SwapWithAgent.FullName : null,
                    ReviewedBy    = s.ReviewedBy != null ? s.ReviewedBy.FullName : null,
                    s.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // POST request shift swap
        group.MapPost("/", async ([FromBody] ShiftSwapRequestDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var req = new ShiftSwapRequest
            {
                TenantId       = tc.TenantId,
                RequestedById  = callerId,
                SwapWithAgentId = dto.SwapWithAgentId,
                SwapDate       = dto.SwapDate.Date,
                Reason         = dto.Reason
            };
            db.ShiftSwapRequests.Add(req);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shift-swap/{req.Id}", new { req.Id });
        });

        // PUT review (admin/manager)
        group.MapPut("/{id:guid}/review", async (Guid id, [FromBody] ReviewShiftSwapDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var req = await db.ShiftSwapRequests.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tc.TenantId);
            if (req == null) return Results.NotFound();
            if (req.Status != ShiftSwapStatus.Pending) return Results.BadRequest("Already reviewed.");

            req.Status        = dto.Approve ? ShiftSwapStatus.Approved : ShiftSwapStatus.Rejected;
            req.ReviewedById  = callerId;
            req.ReviewedAt    = DateTime.UtcNow;
            req.ReviewerNotes = dto.Notes;
            await db.SaveChangesAsync();
            return Results.Ok(new { req.Id, req.Status });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // POST log work mode (WFH/Office/Field)
        group.MapPost("/work-mode", async ([FromBody] WorkModeDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var date = dto.Date?.Date ?? DateTime.UtcNow.Date;

            var existing = await db.WorkModeLogs.FirstOrDefaultAsync(
                w => w.TenantId == tc.TenantId && w.AgentId == callerId && w.Date == date);
            if (existing != null) { existing.WorkMode = dto.WorkMode; }
            else db.WorkModeLogs.Add(new WorkModeLog { TenantId = tc.TenantId, AgentId = callerId, WorkMode = dto.WorkMode, Date = date });

            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // GET work mode logs
        group.MapGet("/work-mode", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] string? from, [FromQuery] string? to) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            var targetId = (agentId.HasValue && (role == "admin" || role == "manager")) ? agentId.Value : callerId;

            var query = db.WorkModeLogs.Where(w => w.TenantId == tc.TenantId && w.AgentId == targetId);
            if (DateTime.TryParse(from, out var fd)) query = query.Where(w => w.Date >= fd);
            if (DateTime.TryParse(to, out var td)) query = query.Where(w => w.Date <= td);

            var items = await query.OrderByDescending(w => w.Date)
                .Select(w => new { w.Id, w.Date, w.WorkMode, AgentName = w.Agent.FullName })
                .ToListAsync();

            return Results.Ok(items);
        });
    }
}

public record ShiftSwapRequestDto(Guid? SwapWithAgentId, DateTime SwapDate, string Reason);
public record ReviewShiftSwapDto(bool Approve, string? Notes);
public record WorkModeDto(WorkModeType WorkMode, DateTime? Date);
