using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leads;

[Authorize]
[RequireModule(CrmModule.Pipeline)]
public class PipelineModel : PageModel
{
    public void OnGet() { }
}
