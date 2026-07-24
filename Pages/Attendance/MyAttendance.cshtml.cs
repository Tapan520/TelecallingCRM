using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Attendance;

[Authorize]
public class MyAttendanceModel : PageModel
{
    public void OnGet() { }
}
