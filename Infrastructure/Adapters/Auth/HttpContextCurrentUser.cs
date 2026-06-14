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

    public override int? OpenProjectInstanceId =>
        int.TryParse(Principal?.FindFirstValue("OpenProjectInstanceId"), out var id) ? id : null;

    public override int? OpenProjectUserId =>
        int.TryParse(Principal?.FindFirstValue("OpenProjectUserId"), out var id) ? id : null;
}
