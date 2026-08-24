using Application.Ports.Auth;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.OAuth;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;
using User = Domain.Entities.OpenProjectEntities.User.User;

namespace Tests.Infrastructure.Adapters.UseCases.Auth;

public class OAuthLoginCommandTests
{
    private static TrackingTasksDbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TrackingTasksDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static Token BuildToken() => new()
    {
        AccessToken = "at-123",
        RefreshToken = "rt-456",
        TokenType = "Bearer",
        ExpiresIn = 7200,
        Scope = "api_v3",
        CreatedAt = 1700000000
    };

    private static OAuthLoginCommandImpl BuildCommand(
        TrackingTasksDbContext db,
        Mock<UserManager<ApplicationUser>> userManagerMock,
        Mock<IOAuthService>? oAuthServiceMock = null,
        Mock<IApiKeyEncryptorService>? encryptorMock = null,
        Mock<IAuthAuditLogger>? auditLoggerMock = null)
    {
        return new OAuthLoginCommandImpl(
            (oAuthServiceMock ?? new Mock<IOAuthService>()).Object,
            (encryptorMock ?? new Mock<IApiKeyEncryptorService>()).Object,
            (auditLoggerMock ?? new Mock<IAuthAuditLogger>()).Object,
            db,
            userManagerMock.Object,
            new Mock<ILogger<OAuthLoginCommandImpl>>().Object);
    }

    [Fact]
    public async Task ExecuteAsync_NewUser_ProvisionsApplicationUserWithoutPassword()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_NewUser_ProvisionsApplicationUserWithoutPassword));
        var instance = new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" };
        await db.OpenProjectInstances.AddAsync(instance);
        await db.SaveChangesAsync();

        var opUser = new User { Id = 42, Name = "Daniel Salazar", Email = "daniel@test.com" };
        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock
            .Setup(x => x.OAuthCallback("code", "state"))
            .ReturnsAsync((opUser, BuildToken(), instance.Id));

        var userManagerMock = BuildUserManagerMock();
        userManagerMock
            .Setup(x => x.Users)
            .Returns(db.Users);
        userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => u.Id = "generated-id")
            .ReturnsAsync(IdentityResult.Success);

        var encryptorMock = new Mock<IApiKeyEncryptorService>();
        encryptorMock.Setup(x => x.Protect(It.IsAny<string>())).Returns((string s) => $"enc-{s}");

        var command = BuildCommand(db, userManagerMock, oAuthServiceMock, encryptorMock);

        var result = await command.ExecuteAsync("code", "state", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("generated-id", result.Data!.UserId);
        Assert.Equal(opUser.Email, result.Data.Email);
        Assert.Equal(instance.BaseUrl, result.Data.OpenProjectInstanceUrl);

        userManagerMock.Verify(x => x.CreateAsync(
            It.Is<ApplicationUser>(u => u.OpenProjectUserId == opUser.Id
                                      && u.OpenProjectInstanceId == instance.Id
                                      && u.AuthMethod == AuthMethod.OAuth)),
            Times.Once);

        var credential = await db.OAuthCredentials.FirstOrDefaultAsync(c => c.UserId == "generated-id");
        Assert.NotNull(credential);
        Assert.Equal("enc-at-123", credential!.EncryptedOAuthAccessToken);
        Assert.Equal("enc-rt-456", credential.EncryptedOAuthRefreshToken);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingUser_ResolvesInsteadOfCreating()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_ExistingUser_ResolvesInsteadOfCreating));
        var instance = new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" };
        await db.OpenProjectInstances.AddAsync(instance);
        await db.SaveChangesAsync();

        var opUser = new User { Id = 42, Name = "Daniel Salazar", Email = "daniel@test.com" };
        var existingUser = new ApplicationUser
        {
            Id = "existing-id",
            Email = opUser.Email,
            OpenProjectUserId = opUser.Id,
            OpenProjectInstanceId = instance.Id,
            OpenProjectInstanceBaseUrl = instance.BaseUrl
        };

        await db.Users.AddAsync(existingUser);
        await db.SaveChangesAsync();

        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock
            .Setup(x => x.OAuthCallback("code", "state"))
            .ReturnsAsync((opUser, BuildToken(), instance.Id));

        var userManagerMock = BuildUserManagerMock();
        userManagerMock
            .Setup(x => x.Users)
            .Returns(db.Users);

        var encryptorMock = new Mock<IApiKeyEncryptorService>();
        encryptorMock.Setup(x => x.Protect(It.IsAny<string>())).Returns((string s) => $"enc-{s}");

        var command = BuildCommand(db, userManagerMock, oAuthServiceMock, encryptorMock);

        var result = await command.ExecuteAsync("code", "state", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("existing-id", result.Data!.UserId);
        userManagerMock.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingUser_UpdatesExistingCredentialInsteadOfDuplicating()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_ExistingUser_UpdatesExistingCredentialInsteadOfDuplicating));
        var instance = new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" };
        var existingUser = new ApplicationUser { Id = "existing-id", Email = "daniel@test.com", OpenProjectUserId = 42, OpenProjectInstanceId = instance.Id, OpenProjectInstanceBaseUrl = instance.BaseUrl };
        await db.OpenProjectInstances.AddAsync(instance);
        await db.OAuthCredentials.AddAsync(new OAuthCredential
        {
            UserId = existingUser.Id,
            ApplicationUser = existingUser,
            EncryptedOAuthAccessToken = "old-token",
            OAuthScope = "old-scope"
        });
        await db.SaveChangesAsync();

        var opUser = new User { Id = 42, Name = "Daniel Salazar", Email = existingUser.Email };
        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock
            .Setup(x => x.OAuthCallback("code", "state"))
            .ReturnsAsync((opUser, BuildToken(), instance.Id));

        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Setup(x => x.Users).Returns(db.Users);

        var encryptorMock = new Mock<IApiKeyEncryptorService>();
        encryptorMock.Setup(x => x.Protect(It.IsAny<string>())).Returns((string s) => $"enc-{s}");

        var command = BuildCommand(db, userManagerMock, oAuthServiceMock, encryptorMock);

        await command.ExecuteAsync("code", "state", CancellationToken.None);

        var credentials = await db.OAuthCredentials.Where(c => c.UserId == existingUser.Id).ToListAsync();
        Assert.Single(credentials);
        Assert.Equal("enc-at-123", credentials[0].EncryptedOAuthAccessToken);
    }

    [Fact]
    public async Task ExecuteAsync_CreateAsyncFails_ReturnsFailureWithIdentityErrors()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_CreateAsyncFails_ReturnsFailureWithIdentityErrors));
        var instance = new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" };
        await db.OpenProjectInstances.AddAsync(instance);
        await db.SaveChangesAsync();

        var opUser = new User { Id = 42, Name = "Daniel", Email = "daniel@test.com" };
        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock
            .Setup(x => x.OAuthCallback("code", "state"))
            .ReturnsAsync((opUser, BuildToken(), instance.Id));

        var userManagerMock = BuildUserManagerMock();
        userManagerMock.Setup(x => x.Users).Returns(db.Users);
        userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Email ya en uso." }));

        var command = BuildCommand(db, userManagerMock, oAuthServiceMock);

        var result = await command.ExecuteAsync("code", "state", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Email ya en uso.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InstanceNotFoundInDb_ThrowsOpInstanceNotFoundException()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_InstanceNotFoundInDb_ThrowsOpInstanceNotFoundException));
        var opUser = new User { Id = 42, Name = "Daniel" };
        var oAuthServiceMock = new Mock<IOAuthService>();
        oAuthServiceMock
            .Setup(x => x.OAuthCallback("code", "state"))
            .ReturnsAsync((opUser, BuildToken(), 999)); // no existe en la DB

        var userManagerMock = BuildUserManagerMock();
        var command = BuildCommand(db, userManagerMock, oAuthServiceMock);

        await Assert.ThrowsAsync<OpInstanceNotFoundException>(() =>
            command.ExecuteAsync("code", "state", CancellationToken.None));
    }
}
