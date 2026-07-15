using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.SuperAdmin;

[Authorize(Roles = "superadmin")]
public class TenantsModel : PageModel
{
    public void OnGet() { }
}
