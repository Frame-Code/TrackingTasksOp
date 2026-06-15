using System.Security.Claims;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Auth;

public class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("OpenProjectInstanceBaseUrl", user.OpenProjectInstanceBaseUrl ?? string.Empty));
        identity.AddClaim(new Claim("OpenProjectInstanceId", user.OpenProjectInstanceId.ToString()));
        identity.AddClaim(new Claim("OpenProjectUserId", user.OpenProjectUserId.ToString()));
        return identity;
    }
}
