using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Tasks;

[Authorize]
[RequireModule(CrmModule.Tasks)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
