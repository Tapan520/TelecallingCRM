using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Calls;

[Authorize]
public class ScriptsModel : PageModel
{
    public void OnGet() { }
}
