using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TelecallingCRM.Data.Models;

namespace TelecallingCRM.Services;

/// <summary>
/// Extends the default claims factory to inject tenant_id and role into
/// the cookie identity so they are available identically to JWT claims.
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim("tenant_id", user.TenantId?.ToString() ?? string.Empty));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role));

        return identity;
    }
}
