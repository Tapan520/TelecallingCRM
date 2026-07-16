using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leads;

[Authorize]
public class PipelineModel : PageModel
{
    public void OnGet() { }
}
