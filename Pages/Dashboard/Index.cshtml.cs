using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
