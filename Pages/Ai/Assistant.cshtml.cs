using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Ai;

[Authorize]
[RequireModule(CrmModule.AiAssistant)]
public class AssistantModel : PageModel
{
    public void OnGet() { }
}
