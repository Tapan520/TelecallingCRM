using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Campaigns;

[Authorize]
[RequireModule(CrmModule.Campaigns)]
public class AnalyticsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Guid CampaignId => Id;

    public void OnGet() { }
}
