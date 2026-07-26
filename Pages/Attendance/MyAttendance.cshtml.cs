using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Attendance;

[Authorize]
[RequireModule(CrmModule.Attendance)]
public class MyAttendanceModel : PageModel
{
    public void OnGet() { }
}
