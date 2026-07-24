using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Attendance;

[Authorize(Roles = "admin,manager")]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
