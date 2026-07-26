using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Gamification;

[Authorize]
[RequireModule(CrmModule.Gamification)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
