using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.FollowUps;

[Authorize]
[RequireModule(CrmModule.FollowUps)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
