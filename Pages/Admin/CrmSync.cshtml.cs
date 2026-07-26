using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Admin;

[Authorize(Roles = "admin,manager")]
[RequireModule(CrmModule.CrmSync)]
public class CrmSyncModel : PageModel
{
    public void OnGet() { }
}
