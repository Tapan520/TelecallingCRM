using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class LeaveEndpoints
{
    public static void MapLeaveEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leaves")
            .WithTags("Leave Management")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET all leave requests
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] string? status,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var query = db.LeaveRequests.Where(l => l.TenantId == tc.TenantId).AsQueryable();

            if (role != "admin" && role != "manager")
                query = query.Where(l => l.AgentId == callerId);
            else if (agentId.HasValue)
                query = query.Where(l => l.AgentId == agentId.Value);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, out var ls))
                query = query.Where(l => l.Status == ls);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new {
                    l.Id, l.AgentId,
                    AgentName    = l.Agent.FullName,
                    l.LeaveType, l.FromDate, l.ToDate,
                    l.TotalDays, l.IsHalfDay, l.Reason,
                    l.Status, l.ReviewerNotes, l.ReviewedAt,
                    ReviewedBy   = l.ReviewedBy != null ? l.ReviewedBy.FullName : null,
                    l.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // GET leave balance
        group.MapGet("/balance", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] int? year) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            var targetId = (agentId.HasValue && (role == "admin" || role == "manager")) ? agentId.Value : callerId;
            var yr = year ?? DateTime.UtcNow.Year;

            var b = await db.LeaveBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tc.TenantId && b.AgentId == targetId && b.Year == yr);

            if (b == null)
                return Results.Ok(new {
                    AgentId = targetId, Year = yr,
                    SickLeaveTotal = 12, SickLeaveUsed = 0, SickLeaveRemaining = 12,
                    CasualLeaveTotal = 12, CasualLeaveUsed = 0, CasualLeaveRemaining = 12,
                    EarnedLeaveTotal = 15, EarnedLeaveUsed = 0, EarnedLeaveRemaining = 15,
                    CompOffTotal = 0, CompOffUsed = 0, CompOffRemaining = 0
                });

            return Results.Ok(new {
                b.Id, AgentId = b.AgentId, Year = b.Year,
                b.SickLeaveTotal, b.SickLeaveUsed, SickLeaveRemaining = b.SickLeaveTotal - b.SickLeaveUsed,
                b.CasualLeaveTotal, b.CasualLeaveUsed, CasualLeaveRemaining = b.CasualLeaveTotal - b.CasualLeaveUsed,
                b.EarnedLeaveTotal, b.EarnedLeaveUsed, EarnedLeaveRemaining = b.EarnedLeaveTotal - b.EarnedLeaveUsed,
                b.CompOffTotal, b.CompOffUsed, CompOffRemaining = b.CompOffTotal - b.CompOffUsed
            });
        });

        // POST apply for leave
        group.MapPost("/", async ([FromBody] ApplyLeaveDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var targetId = callerId;
            if (dto.AgentId.HasValue && dto.AgentId.Value != callerId)
            {
                if (role != "admin" && role != "manager") return Results.Forbid();
                targetId = dto.AgentId.Value;
            }

            if (dto.FromDate > dto.ToDate)
                return Results.BadRequest("FromDate must be before or equal to ToDate.");

            var days = dto.IsHalfDay ? 1 : (int)(dto.ToDate.Date - dto.FromDate.Date).TotalDays + 1;

            var leave = new LeaveRequest
            {
                TenantId  = tc.TenantId,
                AgentId   = targetId,
                LeaveType = dto.LeaveType,
                FromDate  = dto.FromDate.Date,
                ToDate    = dto.ToDate.Date,
                TotalDays = days,
                IsHalfDay = dto.IsHalfDay,
                Reason    = dto.Reason,
                Status    = LeaveStatus.Pending
            };

            db.LeaveRequests.Add(leave);
            await db.SaveChangesAsync();
            return Results.Created($"/api/leaves/{leave.Id}", new { leave.Id, leave.Status });
        });

        // PUT approve / reject (admin/manager only)
        group.MapPut("/{id:guid}/review", async (Guid id, [FromBody] ReviewLeaveDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var leave = await db.LeaveRequests
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (leave == null) return Results.NotFound();
            if (leave.Status != LeaveStatus.Pending)
                return Results.BadRequest("Only pending requests can be reviewed.");

            leave.Status        = dto.Approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
            leave.ReviewedById  = callerId;
            leave.ReviewedAt    = DateTime.UtcNow;
            leave.ReviewerNotes = dto.Notes;
            leave.UpdatedAt     = DateTime.UtcNow;

            // Deduct balance on approval
            if (dto.Approve && leave.LeaveType != LeaveType.UnpaidLeave && leave.LeaveType != LeaveType.Other)
            {
                var yr = leave.FromDate.Year;
                var bal = await db.LeaveBalances
                    .FirstOrDefaultAsync(b => b.TenantId == tc.TenantId && b.AgentId == leave.AgentId && b.Year == yr);
                if (bal == null)
                {
                    bal = new LeaveBalance { TenantId = tc.TenantId, AgentId = leave.AgentId, Year = yr };
                    db.LeaveBalances.Add(bal);
                }
                switch (leave.LeaveType)
                {
                    case LeaveType.SickLeave:   bal.SickLeaveUsed   += leave.TotalDays; break;
                    case LeaveType.CasualLeave: bal.CasualLeaveUsed += leave.TotalDays; break;
                    case LeaveType.EarnedLeave: bal.EarnedLeaveUsed += leave.TotalDays; break;
                    case LeaveType.CompOff:     bal.CompOffUsed     += leave.TotalDays; break;
                }
                bal.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { leave.Id, leave.Status });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // DELETE cancel leave
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var leave = await db.LeaveRequests
                .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tc.TenantId);
            if (leave == null) return Results.NotFound();
            if (leave.AgentId != callerId && role != "admin" && role != "manager") return Results.Forbid();
            if (leave.Status == LeaveStatus.Approved)
                return Results.BadRequest("Approved leaves cannot be cancelled. Contact your admin.");

            leave.Status    = LeaveStatus.Cancelled;
            leave.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { leave.Id, leave.Status });
        });

        // PUT update leave balance (admin only)
        group.MapPut("/balance/{agentId:guid}", async (Guid agentId, [FromBody] UpdateBalanceDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin") return Results.Forbid();

            var yr = dto.Year ?? DateTime.UtcNow.Year;
            var bal = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.TenantId == tc.TenantId && b.AgentId == agentId && b.Year == yr);
            if (bal == null)
            {
                bal = new LeaveBalance { TenantId = tc.TenantId, AgentId = agentId, Year = yr };
                db.LeaveBalances.Add(bal);
            }

            if (dto.SickLeaveTotal.HasValue)   bal.SickLeaveTotal   = dto.SickLeaveTotal.Value;
            if (dto.CasualLeaveTotal.HasValue) bal.CasualLeaveTotal = dto.CasualLeaveTotal.Value;
            if (dto.EarnedLeaveTotal.HasValue) bal.EarnedLeaveTotal = dto.EarnedLeaveTotal.Value;
            if (dto.CompOffTotal.HasValue)     bal.CompOffTotal     = dto.CompOffTotal.Value;
            bal.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new { bal.Id, bal.Year });
        }).RequireAuthorization(p => p.RequireRole("admin"));
    }
}

public record ApplyLeaveDto(Guid? AgentId, LeaveType LeaveType, DateTime FromDate, DateTime ToDate, bool IsHalfDay, string Reason);
public record ReviewLeaveDto(bool Approve, string? Notes);
public record UpdateBalanceDto(int? Year, int? SickLeaveTotal, int? CasualLeaveTotal, int? EarnedLeaveTotal, int? CompOffTotal);
