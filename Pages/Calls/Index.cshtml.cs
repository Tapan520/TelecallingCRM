using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
[RequireModule(CrmModule.CallLog)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
