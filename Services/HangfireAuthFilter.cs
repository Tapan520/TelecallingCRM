using Hangfire.Dashboard;

namespace TelecallingCRM.Services;

/// <summary>Restricts Hangfire dashboard to authenticated admin/superadmin users only.</summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true &&
               (http.User.IsInRole("admin") || http.User.IsInRole("superadmin"));
    }
}
