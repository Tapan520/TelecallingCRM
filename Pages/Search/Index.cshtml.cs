using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Search;

[Authorize]
[RequireModule(CrmModule.GlobalSearch)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
