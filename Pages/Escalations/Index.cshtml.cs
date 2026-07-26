using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Escalations;

[Authorize(Roles = "admin,manager,superadmin")]
[RequireModule(CrmModule.Escalations)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
