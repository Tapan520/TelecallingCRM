using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.SuperAdmin;

[Authorize(Roles = "superadmin")]
public class AllUsersModel : PageModel
{
    public void OnGet() { }
}
