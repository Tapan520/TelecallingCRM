using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Reports;

[Authorize]
[RequireModule(CrmModule.Leaderboard)]
public class LeaderboardModel : PageModel
{
    public void OnGet() { }
}
