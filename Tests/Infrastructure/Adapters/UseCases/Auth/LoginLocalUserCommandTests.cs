using Application.Dto.Auth;
using Application.Ports.Auth;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Auth;

public class LoginLocalUserCommandTests
{
    private static TrackingTasksDbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(dbName)
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

    private static Mock<SignInManager<ApplicationUser>> BuildSignInManagerMock(
        Mock<UserManager<ApplicationUser>> userManagerMock)
    {
#pragma warning disable CS8625
        return new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null, null, null, null);
#pragma warning restore CS8625
    }

    private static LoginLocalUserCommandImpl BuildCommand(
        TrackingTasksDbContext db,
        Mock<UserManager<ApplicationUser>> userManagerMock,
        Mock<SignInManager<ApplicationUser>> signInManagerMock,
        Mock<IAuthAuditLogger>? loggerMock = null)
    {
        return new LoginLocalUserCommandImpl(
            db,
            loggerMock?.Object ?? new Mock<IAuthAuditLogger>().Object,
            new Mock<ILogger<LoginLocalUserCommandImpl>>().Object,
            userManagerMock.Object,
            signInManagerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ReturnsCredencialesInvalidas()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_UserNotFound_ReturnsCredencialesInvalidas));
        var userManagerMock = BuildUserManagerMock();
        var signInManagerMock = BuildSignInManagerMock(userManagerMock);

        userManagerMock
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var command = BuildCommand(db, userManagerMock, signInManagerMock);
        var request = new LocalLoginRequest { Email = "noexiste@test.com", Password = "Pass123!" };

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Credenciales inválidas", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WrongPassword_ReturnsCredencialesInvalidas()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_WrongPassword_ReturnsCredencialesInvalidas));
        var userManagerMock = BuildUserManagerMock();
        var signInManagerMock = BuildSignInManagerMock(userManagerMock);
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(appUser.Email))
            .ReturnsAsync(appUser);

        signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(appUser, It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Failed);

        var command = BuildCommand(db, userManagerMock, signInManagerMock);
        var request = new LocalLoginRequest { Email = appUser.Email, Password = "wrong-password" };

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Credenciales inválidas", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_LockedOutUser_ReturnsCredencialesInvalidas()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_LockedOutUser_ReturnsCredencialesInvalidas));
        var userManagerMock = BuildUserManagerMock();
        var signInManagerMock = BuildSignInManagerMock(userManagerMock);
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(appUser.Email))
            .ReturnsAsync(appUser);

        signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(appUser, It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.LockedOut);

        var command = BuildCommand(db, userManagerMock, signInManagerMock);
        var request = new LocalLoginRequest { Email = appUser.Email, Password = "Pass123!" };

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Credenciales inválidas", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsSuccessWithCorrectData()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_HappyPath_ReturnsSuccessWithCorrectData));
        var userManagerMock = BuildUserManagerMock();
        var signInManagerMock = BuildSignInManagerMock(userManagerMock);
        var auditLoggerMock = new Mock<IAuthAuditLogger>();

        var instance = new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" };
        await db.OpenProjectInstances.AddAsync(instance);
        await db.SaveChangesAsync();

        var appUser = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@test.com",
            UserName = "user@test.com",
            OpenProjectInstanceId = instance.Id,
            OpenProjectInstanceBaseUrl = instance.BaseUrl
        };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(appUser.Email))
            .ReturnsAsync(appUser);

        signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(appUser, It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Success);

        auditLoggerMock
            .Setup(x => x.LogAsync(It.IsAny<AuditEventType>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand(db, userManagerMock, signInManagerMock, auditLoggerMock);
        var request = new LocalLoginRequest { Email = appUser.Email, Password = "Pass123!" };

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(appUser.Id, result.Data.UserId);
        Assert.Equal(appUser.Email, result.Data.Email);
        Assert.Equal(instance.BaseUrl, result.Data.OpenProjectInstanceUrl);
        Assert.Equal(instance.Id, result.Data.OpenProjectInstanceId);
        auditLoggerMock.Verify(x => x.LogAsync(
            AuditEventType.Login,
            appUser.Id,
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InstanceNotFoundInDb_ThrowsInvalidOperationException()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_InstanceNotFoundInDb_ThrowsInvalidOperationException));
        var userManagerMock = BuildUserManagerMock();
        var signInManagerMock = BuildSignInManagerMock(userManagerMock);

        var appUser = new ApplicationUser
        {
            Id = "user-1",
            Email = "user@test.com",
            OpenProjectInstanceId = 999 // no existe en la DB
        };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(appUser.Email))
            .ReturnsAsync(appUser);

        signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(appUser, It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(SignInResult.Success);

        var command = BuildCommand(db, userManagerMock, signInManagerMock);
        var request = new LocalLoginRequest { Email = appUser.Email, Password = "Pass123!" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(request, CancellationToken.None));
    }
}
