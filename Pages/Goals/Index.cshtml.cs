using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Goals;

[Authorize(Roles = "admin,manager,superadmin")]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
