using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(p => p.RequireRole("admin", "superadmin")).RequireRateLimiting("api");

        group.MapGet("/users", async (TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var users = await db.Users
                .Where(u => u.TenantId == tc.TenantId)
                .Select(u => new
                {
                    u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();
            return Results.Ok(users);
        });

        group.MapPost("/users", async ([FromBody] CreateUserDto dto, TenantContext tc,
            AppDbContext db, UserManager<AppUser> userManager, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();

            // Role hierarchy enforcement: admin can only create manager/agent
            var callerRole = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var allowedRoles = callerRole == "admin"
                ? new[] { "manager", "agent" }
                : new[] { "manager", "agent", "admin" }; // manager can only create agents (enforced below)

            if (callerRole == "manager" && dto.Role != "agent")
                return Results.BadRequest("Managers can only create agent accounts.");

            if (!allowedRoles.Contains(dto.Role))
                return Results.BadRequest($"You are not allowed to create a '{dto.Role}' account.");

            var currentUserCount = await db.Users.CountAsync(u => u.TenantId == tc.TenantId);
            if (currentUserCount >= tc.Tenant!.MaxUsers)
                return Results.BadRequest($"User limit reached. Your plan allows up to {tc.Tenant.MaxUsers} users. Please upgrade.");

            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                TenantId = tc.TenantId,
                Role = dto.Role
            };

            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            return Results.Created($"/api/admin/users/{user.Id}", new { user.Id, user.FullName, user.Email, user.Role });
        });

        group.MapPost("/users/{id:guid}/toggle", async (Guid id, TenantContext tc, AppDbContext db) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tc.TenantId);
            if (user == null) return Results.NotFound();
            user.IsActive = !user.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { user.IsActive });
        });

        group.MapPut("/users/{id:guid}", async (Guid id, [FromBody] UpdateUserDto dto, TenantContext tc,
            AppDbContext db, UserManager<AppUser> userManager, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tc.TenantId);
            if (user == null) return Results.NotFound();

            var callerRole = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var allowedRoles = callerRole == "superadmin"
                ? new[] { "manager", "agent", "admin" }
                : new[] { "manager", "agent" };

            if (!allowedRoles.Contains(dto.Role))
                return Results.BadRequest($"You are not allowed to assign the '{dto.Role}' role.");

            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;
            user.Role = dto.Role;

            if (user.Email != dto.Email)
            {
                var setEmailResult = await userManager.SetEmailAsync(user, dto.Email);
                if (!setEmailResult.Succeeded) return Results.BadRequest(setEmailResult.Errors);
                var setUserNameResult = await userManager.SetUserNameAsync(user, dto.Email);
                if (!setUserNameResult.Succeeded) return Results.BadRequest(setUserNameResult.Errors);
            }

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var removeResult = await userManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded) return Results.BadRequest(removeResult.Errors);
                var addResult = await userManager.AddPasswordAsync(user, dto.NewPassword);
                if (!addResult.Succeeded) return Results.BadRequest(addResult.Errors);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role });
        });

        group.MapDelete("/users/{id:guid}", async (Guid id, TenantContext tc,
            AppDbContext db, UserManager<AppUser> userManager, HttpContext http) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tc.TenantId);
            if (user == null) return Results.NotFound();

            // Prevent self-deletion
            var callerId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (callerId != null && user.Id.ToString() == callerId)
                return Results.BadRequest("You cannot delete your own account.");

            var callerRole = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            if (callerRole == "admin" && user.Role == "admin")
                return Results.BadRequest("Admins cannot delete other admin accounts.");

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded) return Results.BadRequest(result.Errors);
            return Results.NoContent();
        });

        // GET /api/admin/audit-log — activity log for the tenant
        group.MapGet("/audit-log", async (TenantContext tc, AppDbContext db,
            [FromQuery] Guid? leadId, [FromQuery] Guid? userId,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        {
            if (!tc.HasTenant) return Results.Unauthorized();
            var query = db.ActivityLogs
                .Where(a => a.TenantId == tc.TenantId)
                .AsQueryable();

            if (leadId.HasValue) query = query.Where(a => a.LeadId == leadId);
            if (userId.HasValue) query = query.Where(a => a.UserId == userId);

            var total = await query.CountAsync();
            var logs = await query
                .OrderByDescending(a => a.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    a.Id, a.Type, a.Summary, a.Detail, a.OccurredAt,
                    a.LeadId, a.UserId,
                    By = a.User.FullName
                })
                .ToListAsync();

            return Results.Ok(new { total, page, pageSize, logs });
        });
    }
}

public record CreateUserDto(string FullName, string Email, string Role, string Password);
public record UpdateUserDto(string FullName, string Email, string? PhoneNumber, string Role, string? NewPassword);
