using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Messaging;

[Authorize]
[RequireModule(CrmModule.Templates)]
public class TemplatesModel : PageModel
{
    public void OnGet() { }
}
