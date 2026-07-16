using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,manager,superadmin")]
public class AuditLogModel : PageModel
{
    public void OnGet() { }
}
