using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Payments;

[Authorize]
[RequireModule(CrmModule.Invoices)]
public class InvoicesModel : PageModel
{
    public void OnGet() { }
}
