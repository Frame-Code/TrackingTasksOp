using Application.Ports.Auth;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.OAuth;
using Infrastructure.Adapters.Http;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Http;

public class OpenProjectAuthHeaderProviderTests
{
    private sealed class FakeCurrentUser(string? userId, int? instanceId = 1) : CurrentUser
    {
        public override string? UserId { get; } = userId;
        public override bool IsAuthenticated => UserId is not null;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => instanceId;
        public override int? OpenProjectUserId => 7;
    }

    // NoTracking a propósito: replica DbContextExtensions.AddDbContext (la config real) — es
    // justo la causa del riesgo de concurrencia que estos tests verifican (cada "request"
    // simulado usa su propio DbContext, como pasaría con el scoping real por request).
    private static TrackingTasksDbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new TrackingTasksDbContext(options);
    }

    private static OpenProjectAuthHeaderProvider BuildProvider(
        TrackingTasksDbContext db, CurrentUser currentUser, IApiKeyEncryptorService encryptor,
        IOAuthService oAuthService, OAuthRefreshLock refreshLock) =>
        new(db, currentUser, encryptor, oAuthService, refreshLock, NullLogger<OpenProjectAuthHeaderProvider>.Instance);

    private static Mock<IApiKeyEncryptorService> BuildPassthroughEncryptor()
    {
        var mock = new Mock<IApiKeyEncryptorService>();
        mock.Setup(x => x.Protect(It.IsAny<string>())).Returns((string s) => $"enc-{s}");
        mock.Setup(x => x.UnProtect(It.IsAny<string>())).Returns((string s) => s.StartsWith("enc-") ? s[4..] : s);
        return mock;
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_LocalCredentialExists_ReturnsBasic()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_LocalCredentialExists_ReturnsBasic));
        db.Set<LocalCredential>().Add(new LocalCredential { UserId = "user-1", EncryptedApiKey = "enc-my-api-key" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            new Mock<IOAuthService>().Object, new OAuthRefreshLock());

        var header = await provider.GetAuthorizationHeaderAsync();

        Assert.Equal("Basic", header.Scheme);
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_OnlyOAuthCredentialAndNotExpired_ReturnsBearerWithStoredToken()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_OnlyOAuthCredentialAndNotExpired_ReturnsBearerWithStoredToken));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-still-valid",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var oAuthServiceMock = new Mock<IOAuthService>();
        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            oAuthServiceMock.Object, new OAuthRefreshLock());

        var header = await provider.GetAuthorizationHeaderAsync();

        Assert.Equal("Bearer", header.Scheme);
        Assert.Equal("still-valid", header.Parameter);
        oAuthServiceMock.Verify(x => x.RefreshToken(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ExpiredToken_RefreshesAndPersistsNewToken()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_ExpiredToken_RefreshesAndPersistsNewToken));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-expired",
            EncryptedOAuthRefreshToken = "enc-old-refresh",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock.Setup(x => x.RefreshToken("old-refresh", 1)).ReturnsAsync(new Token
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            TokenType = "Bearer",
            ExpiresIn = 7200,
            Scope = "api_v3",
            CreatedAt = 1
        });

        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            oAuthServiceMock.Object, new OAuthRefreshLock());

        var header = await provider.GetAuthorizationHeaderAsync();

        Assert.Equal("Bearer", header.Scheme);
        Assert.Equal("new-access", header.Parameter);

        var updated = await db.Set<OAuthCredential>().AsNoTracking().FirstAsync(x => x.UserId == "user-1");
        Assert.Equal("enc-new-access", updated.EncryptedOAuthAccessToken);
        Assert.Equal("enc-new-refresh", updated.EncryptedOAuthRefreshToken);
        Assert.True(updated.OAuthTokenExpiresAt > DateTime.UtcNow.AddMinutes(30));
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_ExpiredWithNoRefreshToken_ThrowsUnauthorized()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_ExpiredWithNoRefreshToken_ThrowsUnauthorized));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-expired",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            new Mock<IOAuthService>().Object, new OAuthRefreshLock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => provider.GetAuthorizationHeaderAsync());
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_RefreshCallFails_ThrowsUnauthorizedInsteadOfPropagatingRawError()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_RefreshCallFails_ThrowsUnauthorizedInsteadOfPropagatingRawError));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-expired",
            EncryptedOAuthRefreshToken = "enc-old-refresh",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock.Setup(x => x.RefreshToken(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("refresh_token invalidado"));

        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            oAuthServiceMock.Object, new OAuthRefreshLock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => provider.GetAuthorizationHeaderAsync());
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_NoCredentialAtAll_ThrowsUnauthorized()
    {
        var db = BuildDbContext(nameof(GetAuthorizationHeaderAsync_NoCredentialAtAll_ThrowsUnauthorized));
        var provider = BuildProvider(db, new FakeCurrentUser("user-1"), BuildPassthroughEncryptor().Object,
            new Mock<IOAuthService>().Object, new OAuthRefreshLock());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => provider.GetAuthorizationHeaderAsync());
    }

    [Fact]
    public async Task GetAuthorizationHeaderAsync_TwoConcurrentRequestsWithExpiredToken_RefreshesOnlyOnce()
    {
        // El escenario real que motiva el lock: dos requests distintos (cada uno con su propio
        // DbContext scoped, como en producción) ven el mismo token vencido a la vez. Sin el lock
        // por usuario, ambos refrescarían en paralelo — y como Doorkeeper rota el refresh_token
        // al usarlo, el segundo intento fallaría con un refresh_token ya invalidado por el primero.
        var dbName = nameof(GetAuthorizationHeaderAsync_TwoConcurrentRequestsWithExpiredToken_RefreshesOnlyOnce);
        var seedDb = BuildDbContext(dbName);
        seedDb.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-expired",
            EncryptedOAuthRefreshToken = "enc-old-refresh",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            OAuthScope = "api_v3"
        });
        await seedDb.SaveChangesAsync();

        var sharedLock = new OAuthRefreshLock();
        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock.Setup(x => x.RefreshToken("old-refresh", 1))
            .Returns(async () =>
            {
                // Fuerza el solape: sin el lock, ambas llamadas entrarían acá antes de que
                // cualquiera termine de persistir el token nuevo.
                await Task.Delay(100);
                return new Token
                {
                    AccessToken = "new-access", RefreshToken = "new-refresh",
                    TokenType = "Bearer", ExpiresIn = 7200, Scope = "api_v3", CreatedAt = 1
                };
            });

        // Dos "requests" separados: cada uno con su propio DbContext, compartiendo el mismo
        // lock singleton (así se registra en DI: OAuthRefreshLock es Singleton).
        var provider1 = BuildProvider(BuildDbContext(dbName), new FakeCurrentUser("user-1"),
            BuildPassthroughEncryptor().Object, oAuthServiceMock.Object, sharedLock);
        var provider2 = BuildProvider(BuildDbContext(dbName), new FakeCurrentUser("user-1"),
            BuildPassthroughEncryptor().Object, oAuthServiceMock.Object, sharedLock);

        var results = await Task.WhenAll(
            provider1.GetAuthorizationHeaderAsync(),
            provider2.GetAuthorizationHeaderAsync());

        Assert.All(results, h => Assert.Equal("new-access", h.Parameter));
        oAuthServiceMock.Verify(x => x.RefreshToken(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
    }
}
