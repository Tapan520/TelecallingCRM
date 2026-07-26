using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leave;

[Authorize]
[RequireModule(CrmModule.Leaves)]
public class MyLeaveModel : PageModel
{
    public void OnGet() { }
}
