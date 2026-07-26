using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,superadmin")]
[RequireModule(CrmModule.Integrations)]
public class IntegrationsModel : PageModel
{
    public void OnGet() { }
}
