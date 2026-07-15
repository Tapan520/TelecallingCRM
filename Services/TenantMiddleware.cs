using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Middleware that resolves the tenant on every request and populates TenantContext.
/// Supports slug-based resolution (subdomain / header / query) and falls back to the
/// tenant_id claim embedded in the auth cookie.  When claims are stale or missing the
/// user row is looked up from the database so old cookies never cause a blank page.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver,
        TenantContext tenantContext, AppDbContext db)
    {
        // ?? 1. Slug-based resolution (subdomain / X-Tenant-Slug header / ?tenant=) ??
        var slug = await resolver.ResolveSlugAsync(context);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            tenantContext.Tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive);

            tenantContext.IsResolved = true;
            await _next(context);
            return;
        }

        // ?? 2. No slug — user must be authenticated for anything beyond static files ??
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            tenantContext.IsResolved = true;
            await _next(context);
            return;
        }

        // ?? 3. Determine the effective role, preferring the claim then falling back
        //       to a DB lookup so stale / old cookies still work. ??
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                     ?? context.User.FindFirst("role")?.Value
                     ?? string.Empty;

        AppUser? appUser = null;

        // If the role claim is missing or blank, load the user from the DB
        if (string.IsNullOrEmpty(roleClaim))
        {
            appUser = await LoadUserFromDb(context, db);
            roleClaim = appUser?.Role ?? string.Empty;
        }

        // Superadmin has no tenant — nothing more to do
        if (roleClaim.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        {
            tenantContext.IsResolved = true;
            await _next(context);
            return;
        }

        // ?? 4. Resolve tenant — prefer the claim, fall back to DB ??
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            tenantContext.Tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
        }
        else
        {
            // Claim missing or not a valid GUID — load user from DB if not already done
            appUser ??= await LoadUserFromDb(context, db);
            if (appUser?.TenantId != null)
            {
                tenantContext.Tenant = await db.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == appUser.TenantId && t.IsActive);
            }
        }

        tenantContext.IsResolved = true;
        await _next(context);
    }

    private static async Task<AppUser?> LoadUserFromDb(HttpContext context, AppDbContext db)
    {
        var userIdValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var userId))
            return null;

        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }
}
