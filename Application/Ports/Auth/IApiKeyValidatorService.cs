using Domain.Entities.OpenProjectEntities.User;

namespace Application.Ports.Auth;

public interface IApiKeyValidatorService
{
    Task<User> ValidateAsync(string instanceUrl, string apiKey, CancellationToken ct);
}