using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Marks a Razor Page as requiring a specific CrmModule to be enabled for the
/// current tenant. When the module is disabled the user is redirected to
/// /AccessDenied instead of seeing the page.
/// SuperAdmins bypass all module checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequireModuleAttribute : Attribute
{
    public CrmModule Module { get; }
    public RequireModuleAttribute(CrmModule module) => Module = module;
}

/// <summary>
/// Global Razor Pages filter that enforces RequireModuleAttribute.
/// Registered via AddRazorPages().AddMvcOptions() in Program.cs.
/// </summary>
public class ModuleAccessFilter : IAsyncPageFilter
{
    public async Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        => await Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var attr = context.HandlerInstance?.GetType()
                          .GetCustomAttributes(typeof(RequireModuleAttribute), inherit: true)
                          .FirstOrDefault() as RequireModuleAttribute;

        // No attribute — no restriction
        if (attr == null)
        {
            await next();
            return;
        }

        // SuperAdmins are never restricted
        if (context.HttpContext.User.IsInRole("superadmin"))
        {
            await next();
            return;
        }

        // Read the enabled modules set injected by TenantMiddleware
        var enabled = context.HttpContext.Items["EnabledModules"] as HashSet<CrmModule>;

        // No set means tenant has all modules (default opt-out behaviour)
        if (enabled == null || enabled.Contains(attr.Module))
        {
            await next();
            return;
        }

        // Module is disabled — redirect to access denied
        context.Result = new RedirectToPageResult("/AccessDenied");
    }
}
