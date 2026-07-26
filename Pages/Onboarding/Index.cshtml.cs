using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Onboarding;

[Authorize]
[RequireModule(CrmModule.Onboarding)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
