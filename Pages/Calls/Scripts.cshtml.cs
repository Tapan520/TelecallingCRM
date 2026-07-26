using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
[RequireModule(CrmModule.CallScripts)]
public class ScriptsModel : PageModel
{
    public void OnGet() { }
}
