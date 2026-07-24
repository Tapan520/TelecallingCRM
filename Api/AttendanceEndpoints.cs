using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attendance")
            .WithTags("Attendance")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET today's punch status for current user (or agent if admin/manager)
        group.MapGet("/today", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var targetId = (agentId.HasValue && (role == "admin" || role == "manager"))
                ? agentId.Value : callerId;

            var today = DateTime.UtcNow.Date;
            var log = await db.AttendanceLogs
                .AsNoTracking()
                .Where(a => a.TenantId == tc.TenantId && a.AgentId == targetId
                         && a.PunchIn >= today && a.PunchIn < today.AddDays(1))
                .OrderByDescending(a => a.PunchIn)
                .Select(a => new {
                    a.Id, a.AgentId, a.PunchIn, a.PunchOut, a.WorkMinutes,
                    a.Status, a.Notes, a.IsManualEntry,
                    PunchedInBy = a.PunchedInBy.FullName,
                    PunchedOutBy = a.PunchedOutBy != null ? a.PunchedOutBy.FullName : null
                })
                .FirstOrDefaultAsync();

            return Results.Ok(log);
        });

        // POST punch-in (self or on behalf of agent)
        group.MapPost("/punch-in", async ([FromBody] PunchInDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var targetId = callerId;
            var isManual = false;

            if (dto.AgentId.HasValue && dto.AgentId.Value != callerId)
            {
                if (role != "admin" && role != "manager")
                    return Results.Forbid();
                targetId = dto.AgentId.Value;
                isManual = true;
            }

            // Check no open punch-in exists today
            var today = DateTime.UtcNow.Date;
            var existing = await db.AttendanceLogs
                .Where(a => a.TenantId == tc.TenantId && a.AgentId == targetId
                         && a.PunchIn >= today && a.PunchIn < today.AddDays(1)
                         && a.PunchOut == null)
                .FirstOrDefaultAsync();

            if (existing != null)
                return Results.BadRequest("Agent is already punched in.");

            var punchTime = dto.PunchTime ?? DateTime.UtcNow;
            var log = new AttendanceLog
            {
                TenantId    = tc.TenantId,
                AgentId     = targetId,
                PunchIn     = punchTime,
                PunchedInById = callerId,
                IsManualEntry = isManual,
                Notes       = dto.Notes
            };
            db.AttendanceLogs.Add(log);
            await db.SaveChangesAsync();

            return Results.Created($"/api/attendance/{log.Id}", new { log.Id, log.PunchIn, log.IsManualEntry });
        });

        // POST punch-out (self or on behalf of agent)
        group.MapPost("/punch-out", async ([FromBody] PunchOutDto dto, TenantContext tc,
            AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var targetId = callerId;
            var isManual = false;

            if (dto.AgentId.HasValue && dto.AgentId.Value != callerId)
            {
                if (role != "admin" && role != "manager")
                    return Results.Forbid();
                targetId = dto.AgentId.Value;
                isManual = true;
            }

            // Find the open punch-in for today
            var today = DateTime.UtcNow.Date;
            var log = await db.AttendanceLogs
                .Where(a => a.TenantId == tc.TenantId && a.AgentId == targetId
                         && a.PunchIn >= today && a.PunchIn < today.AddDays(1)
                         && a.PunchOut == null)
                .OrderByDescending(a => a.PunchIn)
                .FirstOrDefaultAsync();

            if (log == null)
                return Results.BadRequest("No open punch-in found for today.");

            var punchOutTime = dto.PunchTime ?? DateTime.UtcNow;
            log.PunchOut      = punchOutTime;
            log.WorkMinutes   = (int)(punchOutTime - log.PunchIn).TotalMinutes;
            log.PunchedOutById = callerId;
            log.UpdatedAt     = DateTime.UtcNow;
            if (isManual) log.IsManualEntry = true;
            if (!string.IsNullOrWhiteSpace(dto.Notes)) log.Notes = dto.Notes;

            // Determine status
            log.Status = log.WorkMinutes >= 240 ? AttendanceStatus.Present
                       : log.WorkMinutes >= 60  ? AttendanceStatus.HalfDay
                       : AttendanceStatus.Present;

            await db.SaveChangesAsync();
            return Results.Ok(new { log.Id, log.PunchIn, log.PunchOut, log.WorkMinutes, log.Status });
        });

        // GET attendance list - admin/manager sees all, agent sees own
        group.MapGet("/", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] Guid? agentId, [FromQuery] string? from, [FromQuery] string? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var callerId = Guid.Parse(http.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";

            var query = db.AttendanceLogs
                .Where(a => a.TenantId == tc.TenantId)
                .AsQueryable();

            // Agents can only see their own records
            if (role != "admin" && role != "manager")
                query = query.Where(a => a.AgentId == callerId);
            else if (agentId.HasValue)
                query = query.Where(a => a.AgentId == agentId.Value);

            if (DateTime.TryParse(from, out var fromDate))
                query = query.Where(a => a.PunchIn >= fromDate);
            if (DateTime.TryParse(to, out var toDate))
                query = query.Where(a => a.PunchIn < toDate.AddDays(1));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(a => a.PunchIn)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new {
                    a.Id, a.AgentId,
                    AgentName     = a.Agent.FullName,
                    a.PunchIn, a.PunchOut, a.WorkMinutes,
                    a.Status, a.Notes, a.IsManualEntry,
                    PunchedInBy   = a.PunchedInBy.FullName,
                    PunchedOutBy  = a.PunchedOutBy != null ? a.PunchedOutBy.FullName : null,
                    a.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, items });
        });

        // PUT edit/correct an entry (admin/manager only)
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] AttendanceEditDto dto,
            TenantContext tc, AppDbContext db, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var log = await db.AttendanceLogs
                .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tc.TenantId);
            if (log == null) return Results.NotFound();

            log.PunchIn     = dto.PunchIn;
            log.PunchOut    = dto.PunchOut;
            log.Notes       = dto.Notes;
            log.IsManualEntry = true;
            log.UpdatedAt   = DateTime.UtcNow;

            if (dto.PunchOut.HasValue)
                log.WorkMinutes = (int)(dto.PunchOut.Value - dto.PunchIn).TotalMinutes;

            log.Status = log.WorkMinutes >= 240 ? AttendanceStatus.Present
                       : log.WorkMinutes >= 60  ? AttendanceStatus.HalfDay
                       : AttendanceStatus.Present;

            await db.SaveChangesAsync();
            return Results.Ok(new { log.Id, log.PunchIn, log.PunchOut, log.WorkMinutes, log.Status });
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));

        // GET attendance summary (admin/manager only)
        group.MapGet("/summary", async (TenantContext tc, AppDbContext db, HttpContext http,
            [FromQuery] string? from, [FromQuery] string? to) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var role = http.User.FindFirst(ClaimTypes.Role)?.Value ?? http.User.FindFirst("role")?.Value ?? "";
            if (role != "admin" && role != "manager") return Results.Forbid();

            var fromDate = DateTime.TryParse(from, out var fd) ? fd : DateTime.UtcNow.Date.AddDays(-29);
            var toDate   = DateTime.TryParse(to,   out var td) ? td.AddDays(1) : DateTime.UtcNow.Date.AddDays(1);

            var data = await db.AttendanceLogs
                .Where(a => a.TenantId == tc.TenantId
                         && a.PunchIn >= fromDate && a.PunchIn < toDate)
                .GroupBy(a => a.AgentId)
                .Select(g => new {
                    AgentId     = g.Key,
                    AgentName   = g.First().Agent.FullName,
                    TotalDays   = g.Count(),
                    TotalMinutes= g.Sum(a => a.WorkMinutes),
                    ManualEntries = g.Count(a => a.IsManualEntry)
                })
                .OrderBy(x => x.AgentName)
                .ToListAsync();

            return Results.Ok(data);
        }).RequireAuthorization(p => p.RequireRole("admin", "manager"));
    }
}

public record PunchInDto(Guid? AgentId, DateTime? PunchTime, string? Notes);
public record PunchOutDto(Guid? AgentId, DateTime? PunchTime, string? Notes);
public record AttendanceEditDto(DateTime PunchIn, DateTime? PunchOut, string? Notes);
