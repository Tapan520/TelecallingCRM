using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TelecallingCRM.Services;

/// <summary>Login endpoint: 10 requests per minute per IP address (raised to 30 for dev).</summary>
public class LoginRateLimitPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isDev = httpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = isDev ? 30 : 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    }
}

/// <summary>API endpoints: 120 req/min per user in prod, 600 in dev.</summary>
public class ApiRateLimitPolicy : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var key = httpContext.User.Identity?.Name
                  ?? httpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "anon";
        var isDev = httpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = isDev ? 600 : 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    }
}
