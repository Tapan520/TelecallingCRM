using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Documents;

[Authorize]
[RequireModule(CrmModule.Documents)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
