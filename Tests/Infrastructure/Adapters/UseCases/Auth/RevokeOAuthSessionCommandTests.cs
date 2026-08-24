using Application.Ports.Auth;
using Application.Ports.Services;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Auth;

public class RevokeOAuthSessionCommandTests
{
    private sealed class FakeCurrentUser(string? userId, int? instanceId = 1) : CurrentUser
    {
        public override string? UserId { get; } = userId;
        public override bool IsAuthenticated => UserId is not null;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => instanceId;
        public override int? OpenProjectUserId => 7;
    }

    private static TrackingTasksDbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new TrackingTasksDbContext(options);
    }

    private static Mock<IApiKeyEncryptorService> BuildPassthroughEncryptor()
    {
        var mock = new Mock<IApiKeyEncryptorService>();
        mock.Setup(x => x.UnProtect(It.IsAny<string>())).Returns((string s) => s.StartsWith("enc-") ? s[4..] : s);
        return mock;
    }

    [Fact]
    public async Task Execute_UserHasOAuthCredential_RevokesAndDeletesIt()
    {
        var db = BuildDbContext(nameof(Execute_UserHasOAuthCredential_RevokesAndDeletesIt));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-my-token",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var oAuthServiceMock = new Mock<IOAuthService>();
        var command = new RevokeOAuthSessionCommandImpl(
            db, new FakeCurrentUser("user-1"), oAuthServiceMock.Object,
            BuildPassthroughEncryptor().Object, NullLogger<RevokeOAuthSessionCommandImpl>.Instance);

        await command.Execute();

        oAuthServiceMock.Verify(x => x.RevokeToken("my-token", 1), Times.Once);
        Assert.False(await db.Set<OAuthCredential>().AnyAsync(x => x.UserId == "user-1"));
    }

    [Fact]
    public async Task Execute_RevokeCallFails_StillDeletesCredentialLocally()
    {
        // Best-effort: si OpenProject no responde, igual queremos que el logout local no quede
        // con una credencial muerta colgando (el próximo login por OAuth debe arrancar limpio).
        var db = BuildDbContext(nameof(Execute_RevokeCallFails_StillDeletesCredentialLocally));
        db.Set<OAuthCredential>().Add(new OAuthCredential
        {
            UserId = "user-1",
            EncryptedOAuthAccessToken = "enc-my-token",
            OAuthTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            OAuthScope = "api_v3"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock.Setup(x => x.RevokeToken(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new HttpRequestException("no se pudo conectar"));

        var command = new RevokeOAuthSessionCommandImpl(
            db, new FakeCurrentUser("user-1"), oAuthServiceMock.Object,
            BuildPassthroughEncryptor().Object, NullLogger<RevokeOAuthSessionCommandImpl>.Instance);

        await command.Execute();

        Assert.False(await db.Set<OAuthCredential>().AnyAsync(x => x.UserId == "user-1"));
    }

    [Fact]
    public async Task Execute_UserHasNoOAuthCredential_DoesNothing()
    {
        var db = BuildDbContext(nameof(Execute_UserHasNoOAuthCredential_DoesNothing));
        var oAuthServiceMock = new Mock<IOAuthService>();
        var command = new RevokeOAuthSessionCommandImpl(
            db, new FakeCurrentUser("user-1"), oAuthServiceMock.Object,
            BuildPassthroughEncryptor().Object, NullLogger<RevokeOAuthSessionCommandImpl>.Instance);

        await command.Execute();

        oAuthServiceMock.Verify(x => x.RevokeToken(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Execute_NoAuthenticatedUser_DoesNothing()
    {
        var db = BuildDbContext(nameof(Execute_NoAuthenticatedUser_DoesNothing));
        var oAuthServiceMock = new Mock<IOAuthService>();
        var command = new RevokeOAuthSessionCommandImpl(
            db, new FakeCurrentUser(null), oAuthServiceMock.Object,
            BuildPassthroughEncryptor().Object, NullLogger<RevokeOAuthSessionCommandImpl>.Instance);

        await command.Execute(); // no debe tirar

        oAuthServiceMock.Verify(x => x.RevokeToken(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
