using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Meetings;

[Authorize]
[RequireModule(CrmModule.Meetings)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
