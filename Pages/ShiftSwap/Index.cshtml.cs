using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.ShiftSwap;

[Authorize]
[RequireModule(CrmModule.ShiftSwap)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
