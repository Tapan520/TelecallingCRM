using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Ai;

[Authorize]
[RequireModule(CrmModule.AiInsights)]
public class InsightsModel : PageModel
{
    public void OnGet() { }
}
