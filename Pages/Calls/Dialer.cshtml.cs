using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
[RequireModule(CrmModule.Dialer)]
public class DialerModel : PageModel
{
    public string CurrentUserId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public Guid? LeadId { get; set; }

    public void OnGet()
    {
        CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}
