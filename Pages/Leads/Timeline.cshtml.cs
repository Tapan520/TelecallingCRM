using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leads;

[Authorize]
public class TimelineModel : PageModel
{
    public Guid LeadId { get; set; }

    public void OnGet(Guid leadId)
    {
        LeadId = leadId;
    }
}
