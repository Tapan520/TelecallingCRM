using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Api;

public static class SuperAdminEndpoints
{
    public static void MapSuperAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/superadmin")
            .WithTags("SuperAdmin")
            .RequireAuthorization(p => p.RequireRole("superadmin"))
            .RequireRateLimiting("api");

        // GET all tenants
        group.MapGet("/tenants", async (AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null) =>
        {
            var query = db.Tenants.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(t => t.Name.Contains(q) || t.Slug.Contains(q));

            var total = await query.CountAsync();
            var tenants = await query
                .Select(t => new
                {
                    t.Id, t.Name, t.Slug, t.Plan, t.IsActive,
                    t.MaxUsers, t.MaxLeads, t.CreatedAt,
                    UserCount = db.Users.Count(u => u.TenantId == t.Id),
                    LeadCount = db.Leads.Count(l => l.TenantId == t.Id)
                })
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, tenants });
        });

        // POST create a new tenant + its first admin user
        group.MapPost("/tenants", async (
            [FromBody] CreateTenantDto dto,
            AppDbContext db,
            UserManager<AppUser> userManager) =>
        {
            if (await db.Tenants.AnyAsync(t => t.Slug == dto.Slug))
                return Results.BadRequest("Tenant slug is already taken.");

            if (await userManager.FindByEmailAsync(dto.AdminEmail) != null)
                return Results.BadRequest("A user with that email already exists.");

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = dto.CompanyName,
                Slug = dto.Slug.ToLower().Trim(),
                Plan = dto.Plan,
                IsActive = true,
                MaxUsers = dto.MaxUsers,
                MaxLeads = dto.MaxLeads,
                CreatedAt = DateTime.UtcNow
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var admin = new AppUser
            {
                UserName = dto.AdminEmail,
                Email = dto.AdminEmail,
                FullName = dto.AdminFullName,
                TenantId = tenant.Id,
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(admin, dto.AdminPassword);
            if (!result.Succeeded)
            {
                db.Tenants.Remove(tenant);
                await db.SaveChangesAsync();
                return Results.BadRequest(result.Errors);
            }

            return Results.Created($"/api/superadmin/tenants/{tenant.Id}", new
            {
                TenantId = tenant.Id, TenantName = tenant.Name, TenantSlug = tenant.Slug, TenantPlan = tenant.Plan,
                AdminId = admin.Id, AdminEmail = admin.Email
            });
        });

        // PUT update tenant plan/limits
        group.MapPut("/tenants/{id:guid}", async (Guid id, [FromBody] UpdateTenantDto dto, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant == null) return Results.NotFound();

            tenant.Plan = dto.Plan;
            tenant.MaxUsers = dto.MaxUsers;
            tenant.MaxLeads = dto.MaxLeads;
            await db.SaveChangesAsync();
            return Results.Ok(new { tenant.Id, tenant.Plan, tenant.MaxUsers, tenant.MaxLeads });
        });

        // POST suspend / unsuspend a tenant
        group.MapPost("/tenants/{id:guid}/toggle", async (Guid id, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant == null) return Results.NotFound();
            tenant.IsActive = !tenant.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { tenant.IsActive });
        });

        // DELETE permanently remove a tenant and all its data
        group.MapDelete("/tenants/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FindAsync(id);
            if (tenant == null) return Results.NotFound();
            db.Tenants.Remove(tenant); // cascade deletes users, leads, campaigns, calls
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // GET all users across all tenants
        group.MapGet("/users", async (AppDbContext db,
            [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? q = null) =>
        {
            var query = db.Users.Include(u => u.Tenant).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.FullName.Contains(q) || u.Email.Contains(q));

            var total = await query.CountAsync();
            var users = await query
                .Select(u => new
                {
                    u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt,
                    TenantName = u.Tenant != null ? u.Tenant.Name : "\u2014 Platform \u2014",
                    TenantSlug = u.Tenant != null ? u.Tenant.Slug : null,
                    TenantId = u.TenantId
                })
                .OrderBy(u => u.TenantName).ThenBy(u => u.FullName)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync();
            return Results.Ok(new { total, page, pageSize, users });
        });

        // POST create a new user under any tenant
        group.MapPost("/users", async ([FromBody] SuperAdminCreateUserDto dto,
            AppDbContext db, UserManager<AppUser> userManager) =>
        {
            if (!await db.Tenants.AnyAsync(t => t.Id == dto.TenantId))
                return Results.BadRequest("Tenant not found.");

            if (await userManager.FindByEmailAsync(dto.Email) != null)
                return Results.BadRequest("A user with that email already exists.");

            var user = new AppUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                TenantId = dto.TenantId,
                Role = dto.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            return Results.Created($"/api/superadmin/users/{user.Id}",
                new { user.Id, user.FullName, user.Email, user.Role, user.TenantId });
        });

        // PUT update any user
        group.MapPut("/users/{id:guid}", async (Guid id, [FromBody] SuperAdminUpdateUserDto dto,
            AppDbContext db, UserManager<AppUser> userManager) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

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

        // POST suspend / activate a user
        group.MapPost("/users/{id:guid}/toggle", async (Guid id, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();
            user.IsActive = !user.IsActive;
            await db.SaveChangesAsync();
            return Results.Ok(new { user.IsActive });
        });

        // DELETE a user
        group.MapDelete("/users/{id:guid}", async (Guid id, AppDbContext db,
            UserManager<AppUser> userManager, HttpContext http) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return Results.NotFound();

            var callerId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (callerId != null && user.Id.ToString() == callerId)
                return Results.BadRequest("You cannot delete your own account.");

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded) return Results.BadRequest(result.Errors);
            return Results.NoContent();
        });
    }
}

public record CreateTenantDto(
    string CompanyName, string Slug, string Plan,
    int MaxUsers, int MaxLeads,
    string AdminFullName, string AdminEmail, string AdminPassword);

public record UpdateTenantDto(string Plan, int MaxUsers, int MaxLeads);

public record SuperAdminCreateUserDto(
    string FullName, string Email, string? PhoneNumber,
    string Role, string Password, Guid TenantId);

public record SuperAdminUpdateUserDto(
string FullName, string Email, string? PhoneNumber, string Role, string? NewPassword);
