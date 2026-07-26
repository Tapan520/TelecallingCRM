using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.HolidayCalendar;

[Authorize]
[RequireModule(CrmModule.HolidayCalendar)]
public class IndexModel : PageModel
{
    public void OnGet() { }
}
