using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Surveys;

[Authorize]
[RequireModule(CrmModule.NpsSurveys)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
