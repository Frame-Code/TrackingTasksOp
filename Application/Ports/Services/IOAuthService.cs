using Domain.Entities.OpenProjectEntities.OAuth;
using User = Domain.Entities.OpenProjectEntities.User.User;

namespace Application.Ports.Services;

public interface IOAuthService
{
    public Task<string> GenerateOAuthState(int instanceId);
    public Task<string> GenerateAuthorizeUrl(string state, int instanceId);
    public Task<(User User, Token Token, int InstanceId)> OAuthCallback(string code, string state);

    /// <summary>Canjea un refresh_token vigente por un access_token nuevo (Doorkeeper rota el refresh_token al usarlo).</summary>
    public Task<Token> RefreshToken(string refreshToken, int instanceId);

    /// <summary>Invalida el access_token en OpenProject (logout). Best-effort: el caller decide qué hacer si falla.</summary>
    public Task RevokeToken(string accessToken, int instanceId);
}