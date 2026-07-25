using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Leave;

[Authorize]
public class MyLeaveModel : PageModel
{
    public void OnGet() { }
}
