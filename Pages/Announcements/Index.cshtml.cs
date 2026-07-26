using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Announcements;

[Authorize]
[RequireModule(CrmModule.Announcements)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
