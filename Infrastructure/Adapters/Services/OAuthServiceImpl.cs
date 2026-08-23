using System.Net.Http.Headers;
using Application.Ports.Auth;
using Application.Ports.Cache;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.OAuth;
using Infrastructure.Exceptions;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using JsonSerializer = System.Text.Json.JsonSerializer;
using User = Domain.Entities.OpenProjectEntities.User.User;

namespace Infrastructure.Adapters.Services;

public class OAuthServiceImpl(
    IRedisCache cache,
    IOptions<OAuthSettings> settings,
    IOpInstanceService opInstanceService,
    IHttpClientFactory clientFactory,
    IApiKeyEncryptorService encryptorService
    ) : IOAuthService
{
    public async Task<string> GenerateOAuthState(int instanceId)
    {
        var state = Guid.NewGuid().ToString();
        await cache.Save(state, instanceId, TimeSpan.FromMinutes(15));
        return state;
    }

    public async Task<string> GenerateAuthorizeUrl(string state, int instanceId)
    {
        var instance = await opInstanceService.GetOpInstance(instanceId);
        if (instance?.ClientId == null || instance?.ClientSecret == null)
            throw new OpInstanceNotFoundException("Op Instance Not Found Or Not available");
        
        return $"{instance.BaseUrl}/oauth/authorize?response_type=code&client_id={instance.ClientId}&redirect_uri={settings.Value.RedirectUri}&scope=&prompt=consent";
    }

    public async Task<(User User, Token Token, int InstanceId)> OAuthCallback(string code, string state)
    {
        var cacheState = await cache.Get<int>(state);
        if (cacheState == 0)
            throw new StateOAuthException("State Not Found, please retry login");

        var instance = await opInstanceService.GetOpInstance(cacheState);
        if (instance?.ClientId == null || instance?.ClientSecret == null)
            throw new OpInstanceNotFoundException("Op Instance Not Found Or Not available");

        var client = clientFactory.CreateClient(KeyedServicesNames.OpenProjectValidationHttpClientName);
        client.BaseAddress = new Uri(instance.BaseUrl.TrimEnd('/'));
        client.Timeout = TimeSpan.FromSeconds(20);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var tokenRequestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = instance.ClientId,
            ["client_secret"] = encryptorService.UnProtect(instance.ClientSecret),
            ["code"] = code,
            ["redirect_uri"] = settings.Value.RedirectUri
        });
        var response = await client.PostAsync("/oauth/token", tokenRequestBody);

        if (!response.IsSuccessStatusCode)
            throw new OpenProjectRequestException($"OpenProject respondió {(int)response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        var token = JsonSerializer.Deserialize<Token>(body)
            ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de /oauth/token");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var userResponse = await client.GetAsync("/api/v3/users/me");

        if (!userResponse.IsSuccessStatusCode)
            throw new OpenProjectRequestException($"OpenProject respondió {(int)userResponse.StatusCode} al obtener el usuario");

        var userBody = await userResponse.Content.ReadAsStringAsync();
        var opUser = JsonSerializer.Deserialize<User>(userBody)
            ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de /api/v3/users/me");

        return (opUser, token, cacheState);
    }
}