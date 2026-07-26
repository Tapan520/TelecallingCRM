using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Attendance;

[Authorize(Roles = "admin,manager")]
[RequireModule(CrmModule.Attendance)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
