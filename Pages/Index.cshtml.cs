using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages
{
    public class IndexModel : PageModel
    {
    public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToPage("/Dashboard/Index");
            // Unauthenticated users see the landing page (rendered in Index.cshtml)
            return Page();
        }
    }
}

