using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leads;

[Authorize]
public class DetailModel : PageModel
{
    public Guid LeadId { get; private set; }
    public IActionResult OnGet(Guid id)
    {
        LeadId = id;
        return Page();
    }
}
