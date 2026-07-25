using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leave;

[Authorize(Roles = "admin,manager")]
public class ManageLeaveModel : PageModel
{
    public void OnGet() { }
}
