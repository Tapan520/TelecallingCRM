using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;
namespace TelecallingCRM.Pages.Leave;

[Authorize(Roles = "admin,manager")]
[RequireModule(CrmModule.Leaves)]
public class ManageLeaveModel : PageModel
{
    public void OnGet() { }
}
