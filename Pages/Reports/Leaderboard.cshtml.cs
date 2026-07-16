using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Reports;

[Authorize(Roles = "admin,manager,superadmin")]
public class LeaderboardModel : PageModel
{
    public void OnGet() { }
}
