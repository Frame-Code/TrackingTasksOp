using System.Net;
using System.Text;
using Application.Dto.OpInstance;
using Application.Ports.Auth;
using Application.Ports.Cache;
using Application.Ports.Services;
using Infrastructure.Adapters.Services;
using Infrastructure.Exceptions;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class OAuthServiceImplTests
{
    // created_at/expires_in van como números JSON (no strings) en la respuesta real de
    // Doorkeeper — con comillas, este fixture no hubiera detectado el bug de tipos que
    // rompió el flujo real (Token.CreatedAt estaba declarado como string).
    private const string TokenJson = """
        {
            "access_token": "at-123",
            "token_type": "Bearer",
            "expires_in": 7200,
            "refresh_token": "rt-456",
            "scope": "api_v3",
            "created_at": 1700000000
        }
        """;

    private const string UserJson = """{ "id": 7, "name": "Daniel Salazar", "login": "daniel", "email": "daniel@test.com", "admin": true }""";

    private static (OAuthServiceImpl Service, Mock<IRedisCache> Cache, Mock<IOpInstanceService> Instances,
        Mock<IApiKeyEncryptorService> Encryptor, Func<List<HttpRequestMessage>> Requests) Build(
            GetOpInstance? instance = null,
            HttpStatusCode tokenStatus = HttpStatusCode.OK,
            HttpStatusCode userStatus = HttpStatusCode.OK)
    {
        var requests = new List<HttpRequestMessage>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => requests.Add(req))
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                req.RequestUri!.AbsolutePath == "/oauth/token"
                    ? new HttpResponseMessage { StatusCode = tokenStatus, Content = new StringContent(TokenJson, Encoding.UTF8, "application/json") }
                    : new HttpResponseMessage { StatusCode = userStatus, Content = new StringContent(UserJson, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var cacheMock = new Mock<IRedisCache>();
        var instancesMock = new Mock<IOpInstanceService>();
        instancesMock.Setup(x => x.GetOpInstance(It.IsAny<int>())).ReturnsAsync(instance);

        var encryptorMock = new Mock<IApiKeyEncryptorService>();
        encryptorMock.Setup(x => x.UnProtect(It.IsAny<string>())).Returns((string s) => $"decrypted-{s}");

        var settings = Options.Create(new OAuthSettings { RedirectUri = "http://localhost:5266/api/v1/auth/oauth/callback" });

        var service = new OAuthServiceImpl(cacheMock.Object, settings, instancesMock.Object, factoryMock.Object, encryptorMock.Object);

        return (service, cacheMock, instancesMock, encryptorMock, () => requests);
    }

    private static GetOpInstance ConnectedInstance() =>
        new("http://op.example.com", "client-id-1", "encrypted-secret");

    // ── GenerateOAuthState ──────────────────────────────────────────────────

    [Fact]
    public async Task GenerateOAuthState_SavesInstanceIdInCache()
    {
        var (service, cache, _, _, _) = Build();

        var state = await service.GenerateOAuthState(42);

        Assert.False(string.IsNullOrWhiteSpace(state));
        cache.Verify(c => c.Save(state, 42, TimeSpan.FromMinutes(15)), Times.Once);
    }

    // ── GenerateAuthorizeUrl ────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAuthorizeUrl_HappyPath_BuildsUrlWithClientIdAndRedirectUri()
    {
        var (service, _, _, _, _) = Build(ConnectedInstance());

        var url = await service.GenerateAuthorizeUrl("some-state", 1);

        Assert.StartsWith("http://op.example.com/oauth/authorize", url);
        Assert.Contains("client_id=client-id-1", url);
        // redirect_uri va URL-encodeado como valor de query string.
        Assert.Contains($"redirect_uri={Uri.EscapeDataString("http://localhost:5266/api/v1/auth/oauth/callback")}", url);
        // Regresión: el state generado para CSRF nunca se mandaba a OpenProject, así que
        // nunca volvía en el callback y el login quedaba roto siempre.
        Assert.Contains("state=some-state", url);
    }

    [Fact]
    public async Task GenerateAuthorizeUrl_InstanceNotConnected_Throws()
    {
        var (service, _, _, _, _) = Build(instance: null);

        await Assert.ThrowsAsync<OpInstanceNotFoundException>(() => service.GenerateAuthorizeUrl("state", 1));
    }

    // ── OAuthCallback ───────────────────────────────────────────────────────

    [Fact]
    public async Task OAuthCallback_StateNotInCache_ThrowsStateOAuthException()
    {
        var (service, cache, _, _, _) = Build(ConnectedInstance());
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(0);

        await Assert.ThrowsAsync<StateOAuthException>(() => service.OAuthCallback("code", "unknown-state"));
    }

    [Fact]
    public async Task OAuthCallback_InstanceNoLongerConnected_ThrowsOpInstanceNotFoundException()
    {
        var (service, cache, _, _, _) = Build(instance: null);
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<OpInstanceNotFoundException>(() => service.OAuthCallback("code", "state"));
    }

    [Fact]
    public async Task OAuthCallback_TokenExchangeFails_ThrowsOpenProjectRequestException()
    {
        var (service, cache, _, _, _) = Build(ConnectedInstance(), tokenStatus: HttpStatusCode.BadRequest);
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<OpenProjectRequestException>(() => service.OAuthCallback("code", "state"));
    }

    [Fact]
    public async Task OAuthCallback_UsersMeFails_ThrowsOpenProjectRequestException()
    {
        var (service, cache, _, _, _) = Build(ConnectedInstance(), userStatus: HttpStatusCode.Unauthorized);
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<OpenProjectRequestException>(() => service.OAuthCallback("code", "state"));
    }

    [Fact]
    public async Task OAuthCallback_HappyPath_ReturnsUserTokenAndInstanceId()
    {
        var (service, cache, _, encryptor, getRequests) = Build(ConnectedInstance());
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(99);

        var (user, token, instanceId) = await service.OAuthCallback("auth-code", "state");

        Assert.Equal(7, user.Id);
        Assert.True(user.Admin);
        Assert.Equal("at-123", token.AccessToken);
        Assert.Equal("rt-456", token.RefreshToken);
        Assert.Equal(99, instanceId);
        encryptor.Verify(x => x.UnProtect("encrypted-secret"), Times.Once);
    }

    [Fact]
    public async Task OAuthCallback_TokenExchange_HitsCorrectEndpointWithFormEncodedBodyAndRedirectUri()
    {
        // Regresión: el endpoint era /oauth/access_token (incorrecto), el body iba como JSON
        // en vez de x-www-form-urlencoded, y faltaba redirect_uri — Doorkeeper exige los tres.
        var (service, cache, _, _, getRequests) = Build(ConnectedInstance());
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(1);

        await service.OAuthCallback("auth-code", "state");

        var tokenRequest = getRequests().First(r => r.RequestUri!.AbsolutePath == "/oauth/token");
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Equal("application/x-www-form-urlencoded", tokenRequest.Content!.Headers.ContentType!.MediaType);

        var body = await tokenRequest.Content!.ReadAsStringAsync();
        Assert.Contains("grant_type=authorization_code", body);
        Assert.Contains("code=auth-code", body);
        Assert.Contains("redirect_uri=", body);
    }

    [Fact]
    public async Task OAuthCallback_UsersMeRequest_CarriesBearerAccessToken()
    {
        var (service, cache, _, _, getRequests) = Build(ConnectedInstance());
        cache.Setup(c => c.Get<int>(It.IsAny<string>())).ReturnsAsync(1);

        await service.OAuthCallback("auth-code", "state");

        var userRequest = getRequests().First(r => r.RequestUri!.AbsolutePath == "/api/v3/users/me");
        Assert.Equal("Bearer", userRequest.Headers.Authorization!.Scheme);
        Assert.Equal("at-123", userRequest.Headers.Authorization!.Parameter);
    }
}
