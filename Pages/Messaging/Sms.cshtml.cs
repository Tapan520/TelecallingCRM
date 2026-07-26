using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Messaging;

[Authorize]
[RequireModule(CrmModule.Sms)]
public class SmsModel : PageModel
{
    public void OnGet() { }
}
