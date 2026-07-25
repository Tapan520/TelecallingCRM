using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class HolidayEndpoints
{
    public static void MapHolidayEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/holidays")
            .WithTags("Holidays")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        // GET all holidays for current/specified year
        group.MapGet("/", async (TenantContext tc, AppDbContext db,
            [FromQuery] int? year) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var yr = year ?? DateTime.UtcNow.Year;
            var from = new DateTime(yr, 1, 1);
            var to = new DateTime(yr, 12, 31);
            var items = await db.Holidays
                .Where(h => h.TenantId == tc.TenantId && h.Date >= from && h.Date <= to)
                .OrderBy(h => h.Date)
                .Select(h => new { h.Id, h.Name, h.Date, h.Type, h.Description, h.IsRecurringYearly })
                .ToListAsync();
            return Results.Ok(items);
        });

        // POST create holiday (admin only)
        group.MapPost("/", async ([FromBody] HolidayDto dto, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var holiday = new Holiday
            {
                TenantId = tc.TenantId, Name = dto.Name,
                Date = dto.Date.Date, Type = dto.Type,
                Description = dto.Description, IsRecurringYearly = dto.IsRecurringYearly
            };
            db.Holidays.Add(holiday);
            await db.SaveChangesAsync();
            return Results.Created($"/api/holidays/{holiday.Id}", new { holiday.Id });
        }).RequireAuthorization(p => p.RequireRole("admin"));

        // DELETE holiday (admin only)
        group.MapDelete("/{id:guid}", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var h = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tc.TenantId);
            if (h == null) return Results.NotFound();
            db.Holidays.Remove(h);
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization(p => p.RequireRole("admin"));
    }
}

public record HolidayDto(string Name, DateTime Date, HolidayType Type, string? Description, bool IsRecurringYearly);
