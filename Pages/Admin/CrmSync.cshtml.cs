using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,manager")]
public class CrmSyncModel : PageModel
{
    public void OnGet() { }
}
