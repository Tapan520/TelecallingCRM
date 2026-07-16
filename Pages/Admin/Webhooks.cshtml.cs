using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,superadmin")]
public class WebhooksModel : PageModel
{
    public void OnGet() { }
}
