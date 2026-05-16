using System.Security.Claims;
using Application.Ports.Auth;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Adapters.Auth;

public class HttpContextCurrentUser(IHttpContextAccessor accessor) : CurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public override string? UserId =>
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public override bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public override string? OpenProjectInstanceUrl =>
        Principal?.FindFirstValue("OpenProjectInstanceBaseUrl");
}
