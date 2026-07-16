using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Payments;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
