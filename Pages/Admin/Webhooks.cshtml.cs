using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,superadmin")]
[RequireModule(CrmModule.Webhooks)]
public class WebhooksModel : PageModel
{
    public void OnGet() { }
}
