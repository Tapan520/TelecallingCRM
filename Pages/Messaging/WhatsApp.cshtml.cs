using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Messaging;

[Authorize]
[RequireModule(CrmModule.WhatsApp)]
public class WhatsAppModel : PageModel
{
    public void OnGet() { }
}
