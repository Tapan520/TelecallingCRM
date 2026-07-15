using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Admin;

[Authorize]
public class SettingsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly TenantContext _tenantContext;

    public SettingsModel(AppDbContext db, UserManager<AppUser> userManager, TenantContext tenantContext)
    {
        _db = db;
        _userManager = userManager;
        _tenantContext = tenantContext;
    }

    [BindProperty] public string OrgName { get; set; } = string.Empty;
    [BindProperty] public string? OpenRouterKey { get; set; }
    [BindProperty] public string? PreferredModel { get; set; }
    public string? Plan { get; set; }
    public string? TenantSlug { get; set; }
    public Guid TenantId { get; set; }
    public int MaxUsers { get; set; }
    public int MaxLeads { get; set; }

    private async Task<Tenant?> GetTenantAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return null;
        return await _db.Tenants.FindAsync(user.TenantId);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var tenant = await GetTenantAsync();
        if (tenant == null) return RedirectToPage("/Login");
        OrgName = tenant.Name;
        OpenRouterKey = tenant.OpenRouterApiKey;
        PreferredModel = tenant.PreferredModel;
        Plan = tenant.Plan;
        TenantSlug = tenant.Slug;
        TenantId = tenant.Id;
        MaxUsers = tenant.MaxUsers;
        MaxLeads = tenant.MaxLeads;
        return Page();
    }

    public async Task<IActionResult> OnPostOrganizationAsync()
    {
        var tenant = await GetTenantAsync();
        if (tenant == null) return RedirectToPage("/Login");
        tenant.Name = OrgName;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Organization settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAiAsync()
    {
        var tenant = await GetTenantAsync();
        if (tenant == null) return RedirectToPage("/Login");
        tenant.OpenRouterApiKey = OpenRouterKey;
        tenant.PreferredModel = PreferredModel;
        await _db.SaveChangesAsync();
        TempData["Success"] = "AI settings saved.";
        return RedirectToPage();
    }
}
