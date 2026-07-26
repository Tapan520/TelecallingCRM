using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Expenses;

[Authorize]
[RequireModule(CrmModule.Expenses)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
