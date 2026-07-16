using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TelecallingCRM.Pages.Profile;

[Authorize]
public class IndexModel : PageModel { public void OnGet() { } }
