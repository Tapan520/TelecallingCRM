using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Messaging;

[Authorize]
public class SmsModel : PageModel
{
    public void OnGet() { }
}
