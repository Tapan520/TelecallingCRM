using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Payments;

[Authorize(Roles = "admin,manager")]
[RequireModule(CrmModule.Payments)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
