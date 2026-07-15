using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // POST /api/auth/register
        group.MapPost("/register", async (
            [FromBody] RegisterRequest req,
            AppDbContext db,
            UserManager<AppUser> userManager,
            ITokenService tokenService) =>
        {
            if (await db.Tenants.AnyAsync(t => t.Slug == req.TenantSlug))
                return Results.BadRequest("Tenant slug already taken.");

            var tenant = new Tenant
            {
                Name = req.CompanyName,
                Slug = req.TenantSlug.ToLower().Trim()
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var user = new AppUser
            {
                UserName = req.Email,
                Email = req.Email,
                FullName = req.FullName,
                TenantId = tenant.Id,
                Role = "admin"
            };
            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            var refreshToken = tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new { token, refreshToken, tenantSlug = tenant.Slug, userId = user.Id });
        }).RequireRateLimiting("login");

        // POST /api/auth/login
        group.MapPost("/login", async (
            [FromBody] LoginRequest req,
            AppDbContext db,
            UserManager<AppUser> userManager,
            ITokenService tokenService) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user == null || !user.IsActive)
                return Results.Unauthorized();

            if (!await userManager.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            user.LastLoginAt = DateTime.UtcNow;

            var refreshToken = tokenService.GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new { token, refreshToken, role = user.Role, tenantId = user.TenantId, userId = user.Id, fullName = user.FullName });
        }).RequireRateLimiting("login");

        // POST /api/auth/refresh
        group.MapPost("/refresh", async (
            [FromBody] RefreshRequest req,
            AppDbContext db,
            ITokenService tokenService) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.RefreshToken == req.RefreshToken);
            if (user == null || !user.IsActive)
                return Results.Unauthorized();
            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
                return Results.Unauthorized();

            var newToken = tokenService.GenerateToken(user);
            var newRefresh = tokenService.GenerateRefreshToken();
            user.RefreshToken = newRefresh;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();

            return Results.Ok(new { token = newToken, refreshToken = newRefresh });
        });

        // POST /api/auth/revoke (logout — invalidate refresh token)
        group.MapPost("/revoke", async (HttpContext http, AppDbContext db) =>
        {
            var userId = http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Results.Unauthorized();
            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user == null) return Results.Unauthorized();
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await db.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization();
    }
}

public record RegisterRequest(string CompanyName, string TenantSlug, string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

