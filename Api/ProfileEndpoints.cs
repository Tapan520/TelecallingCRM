using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization()
            .RequireRateLimiting("api");

        group.MapGet("/", async (AppDbContext db, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var user = await db.Users.FindAsync(userId);
            if (user == null) return Results.Unauthorized();
            return Results.Ok(new {
                user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role,
                user.CreatedAt, user.LastLoginAt, user.TenantId
            });
        });

        group.MapPut("/", async ([FromBody] UpdateProfileDto dto,
            AppDbContext db, UserManager<AppUser> userManager, HttpContext http) =>
        {
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var user = await db.Users.FindAsync(userId);
            if (user == null) return Results.Unauthorized();

            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;

            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                if (string.IsNullOrEmpty(dto.CurrentPassword))
                    return Results.BadRequest("Current password is required to set a new password.");
                var ok = await userManager.CheckPasswordAsync(user, dto.CurrentPassword);
                if (!ok) return Results.BadRequest("Current password is incorrect.");
                var remove = await userManager.RemovePasswordAsync(user);
                if (!remove.Succeeded) return Results.BadRequest(remove.Errors);
                var add = await userManager.AddPasswordAsync(user, dto.NewPassword);
                if (!add.Succeeded) return Results.BadRequest(add.Errors);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber });
        });

        group.MapGet("/stats", async (AppDbContext db, HttpContext http, TenantContext tc) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return Results.Ok(new {
                callsThisMonth = await db.Calls.CountAsync(c => c.AgentId == userId && c.StartedAt >= monthStart),
                conversionsThisMonth = await db.Calls.CountAsync(c => c.AgentId == userId && c.StartedAt >= monthStart && c.Outcome == CallOutcome.Converted),
                talkSecondsThisMonth = await db.Calls.Where(c => c.AgentId == userId && c.StartedAt >= monthStart).SumAsync(c => (long)c.DurationSeconds),
                assignedLeads = await db.Leads.CountAsync(l => l.AssignedToId == userId && l.TenantId == tc.TenantId),
                pendingFollowUps = await db.FollowUps.CountAsync(f => f.AssignedToId == userId && f.Status == FollowUpStatus.Pending),
                pendingTasks = await db.Tasks.CountAsync(t => t.AssignedToId == userId && t.Status == TelecallingCRM.Data.Models.TaskStatus.Pending)
            });
        });
    }
}

public record UpdateProfileDto(string FullName, string? PhoneNumber,
    string? CurrentPassword, string? NewPassword);
