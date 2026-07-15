using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
public class DialerModel : PageModel
{
    public string CurrentUserId { get; set; } = string.Empty;

    public void OnGet()
    {
        CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}
