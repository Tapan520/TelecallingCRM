using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TelecallingCRM.Data.Models;
using TelecallingCRM.Services;

namespace TelecallingCRM.Pages.Leads;

[Authorize]
[RequireModule(CrmModule.Leads)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
