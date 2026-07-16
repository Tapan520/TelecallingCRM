using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace TelecallingCRM.Pages.Quotes;

[Authorize]
public class IndexModel : PageModel
{
    public string CurrentUserId { get; private set; } = string.Empty;

    public void OnGet()
    {
        CurrentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}
