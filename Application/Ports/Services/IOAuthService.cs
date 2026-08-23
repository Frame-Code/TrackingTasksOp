using Domain.Entities.OpenProjectEntities.OAuth;
using User = Domain.Entities.OpenProjectEntities.User.User;

namespace Application.Ports.Services;

public interface IOAuthService
{
    public Task<string> GenerateOAuthState(int instanceId);
    public Task<string> GenerateAuthorizeUrl(string state, int instanceId);
    public Task<(User User, Token Token, int InstanceId)> OAuthCallback(string code, string state);
}