using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Commissions;

[Authorize]
[RequireModule(CrmModule.Commissions)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
