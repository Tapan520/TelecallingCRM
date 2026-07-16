using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Automation;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
