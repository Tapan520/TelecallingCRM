using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using TelecallingCRM.Data;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
[RequireModule(CrmModule.Dialer)]
public class DialerModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly TenantContext _tc;

    public DialerModel(AppDbContext db, TenantContext tc)
    {
        _db = db;
        _tc = tc;
    }

    public string CurrentUserId { get; set; } = string.Empty;
    public bool PhoneMaskingEnabled { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? LeadId { get; set; }

    public async Task OnGetAsync()
    {
        CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        if (_tc.HasTenant)
        {
            var tenant = await _db.Tenants.FindAsync(_tc.TenantId);
            var isAdminOrAbove = User.IsInRole("admin") || User.IsInRole("superadmin");
            PhoneMaskingEnabled = tenant?.PhoneMaskingEnabled == true && !isAdminOrAbove;
        }
    }
}
