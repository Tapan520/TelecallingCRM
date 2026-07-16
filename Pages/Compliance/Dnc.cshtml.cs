using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Compliance;

[Authorize]
public class DncModel : PageModel
{
    public void OnGet() { }
}
