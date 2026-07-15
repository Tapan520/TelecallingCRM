namespace TelecallingCRM.Services;

/// <summary>
/// Resolves the current tenant from the HTTP request.
/// Supports subdomain-based and X-Tenant-Slug header resolution.
/// </summary>
public interface ITenantResolver
{
    Task<string?> ResolveSlugAsync(HttpContext context);
}

public class TenantResolver : ITenantResolver
{
    public Task<string?> ResolveSlugAsync(HttpContext context)
    {
        // 1. Check X-Tenant-Slug header (API clients, mobile)
        if (context.Request.Headers.TryGetValue("X-Tenant-Slug", out var headerSlug))
            return Task.FromResult<string?>(headerSlug.ToString());

        // 2. Subdomain: {slug}.yourdomain.com
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
            return Task.FromResult<string?>(parts[0]);

        // 3. Query string fallback (dev/testing)
        if (context.Request.Query.TryGetValue("tenant", out var querySlug))
            return Task.FromResult<string?>(querySlug.ToString());

        return Task.FromResult<string?>(null);
    }
}
