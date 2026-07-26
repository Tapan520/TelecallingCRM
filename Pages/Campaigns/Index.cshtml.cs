using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Campaigns;

[Authorize]
[RequireModule(CrmModule.Campaigns)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
