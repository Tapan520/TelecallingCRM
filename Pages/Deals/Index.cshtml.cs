using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Deals;

[Authorize]
[RequireModule(CrmModule.Deals)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
