using Application.Dto.Auth;
using Application.Ports.Auth;
using Domain.Entities.OpenProjectEntities.User;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Auth;

public class RegisterLocalUserCommandTests
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

    private static RegisterLocalUserCommandImpl BuildCommand(
        TrackingTasksDbContext db,
        Mock<UserManager<ApplicationUser>> userManagerMock,
        Mock<IApiKeyValidatorService>? validatorMock = null,
        Mock<IApiKeyEncryptorService>? encryptorMock = null,
        Mock<IAuthAuditLogger>? auditLoggerMock = null)
    {
        var validator = validatorMock ?? new Mock<IApiKeyValidatorService>();
        var encryptor = encryptorMock ?? new Mock<IApiKeyEncryptorService>();
        var auditLogger = auditLoggerMock ?? new Mock<IAuthAuditLogger>();

        return new RegisterLocalUserCommandImpl(
            validator.Object,
            encryptor.Object,
            new Mock<ILogger<RegisterLocalUserCommandImpl>>().Object,
            auditLogger.Object,
            db,
            userManagerMock.Object);
    }

    private static LocalRegisterRequest BuildRequest(
        string email = "nuevo@test.com",
        string password = "Pass123!",
        string apiKey = "api-key-123",
        string instanceUrl = "http://op.example.com") =>
        new()
        {
            Email = email,
            Password = password,
            ApiKey = apiKey,
            OpenProjectInstanceUrl = instanceUrl
        };

    [Fact]
    public async Task ExecuteAsync_UserAlreadyExists_ReturnsFailure()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_UserAlreadyExists_ReturnsFailure));
        var userManagerMock = BuildUserManagerMock();
        var existingUser = new ApplicationUser { Email = "existe@test.com" };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(existingUser.Email))
            .ReturnsAsync(existingUser);

        var command = BuildCommand(db, userManagerMock);
        var request = BuildRequest(email: existingUser.Email);

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("El usuario ya se encuentra registrado", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UserAlreadyExists_DoesNotCallApiKeyValidator()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_UserAlreadyExists_DoesNotCallApiKeyValidator));
        var userManagerMock = BuildUserManagerMock();
        var validatorMock = new Mock<IApiKeyValidatorService>();
        var existingUser = new ApplicationUser { Email = "existe@test.com" };

        userManagerMock
            .Setup(x => x.FindByEmailAsync(existingUser.Email))
            .ReturnsAsync(existingUser);

        var command = BuildCommand(db, userManagerMock, validatorMock);
        var request = BuildRequest(email: existingUser.Email);

        await command.ExecuteAsync(request, CancellationToken.None);

        validatorMock.Verify(x =>
            x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CreateUserFails_ReturnsFailureWithIdentityErrors()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_CreateUserFails_ReturnsFailureWithIdentityErrors));
        var userManagerMock = BuildUserManagerMock();
        var validatorMock = new Mock<IApiKeyValidatorService>();
        var encryptorMock = new Mock<IApiKeyEncryptorService>();

        userManagerMock
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Name = "Test User" });

        var identityErrors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "La contraseña es muy corta." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "La contraseña requiere un dígito." }
        };
        userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(identityErrors));

        var command = BuildCommand(db, userManagerMock, validatorMock, encryptorMock);
        var request = BuildRequest();

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("La contraseña es muy corta.", result.ErrorMessage);
        Assert.Contains("La contraseña requiere un dígito.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ApiKeyValidatorThrows_PropagatesException()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_ApiKeyValidatorThrows_PropagatesException));
        var userManagerMock = BuildUserManagerMock();
        var validatorMock = new Mock<IApiKeyValidatorService>();

        userManagerMock
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("API key inválida"));

        var command = BuildCommand(db, userManagerMock, validatorMock);
        var request = BuildRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_ReturnsSuccessWithCorrectData()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_HappyPath_ReturnsSuccessWithCorrectData));
        var userManagerMock = BuildUserManagerMock();
        var validatorMock = new Mock<IApiKeyValidatorService>();
        var encryptorMock = new Mock<IApiKeyEncryptorService>();
        var auditLoggerMock = new Mock<IAuthAuditLogger>();

        var opUser = new User { Id = 42, Name = "Daniel Salazar" };
        var request = BuildRequest(
            email: "daniel@test.com",
            apiKey: "my-api-key",
            instanceUrl: "http://op.example.com");

        userManagerMock
            .Setup(x => x.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        validatorMock
            .Setup(x => x.ValidateAsync(request.OpenProjectInstanceUrl, request.ApiKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(opUser);

        userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "generated-user-id")
            .ReturnsAsync(IdentityResult.Success);

        encryptorMock
            .Setup(x => x.Protect(request.ApiKey))
            .Returns("encrypted-api-key");

        auditLoggerMock
            .Setup(x => x.LogAsync(It.IsAny<AuditEventType>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = BuildCommand(db, userManagerMock, validatorMock, encryptorMock, auditLoggerMock);

        var result = await command.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(request.Email, result.Data.Email);
        Assert.Equal(request.OpenProjectInstanceUrl, result.Data.OpenProjectInstanceUrl);
        Assert.Equal(opUser.Name, result.Data.Name);
        auditLoggerMock.Verify(x => x.LogAsync(
            AuditEventType.Register,
            It.IsAny<string>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        encryptorMock.Verify(x => x.Protect(request.ApiKey), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_CreatesOpenProjectInstanceWhenNotExists()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_HappyPath_CreatesOpenProjectInstanceWhenNotExists));
        var userManagerMock = BuildUserManagerMock();
        var validatorMock = new Mock<IApiKeyValidatorService>();
        var encryptorMock = new Mock<IApiKeyEncryptorService>();

        const string instanceUrl = "http://new-instance.example.com";

        userManagerMock
            .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        validatorMock
            .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Name = "User" });

        userManagerMock
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "generated-user-id")
            .ReturnsAsync(IdentityResult.Success);

        encryptorMock.Setup(x => x.Protect(It.IsAny<string>())).Returns("encrypted");

        var command = BuildCommand(db, userManagerMock, validatorMock, encryptorMock);
        var request = BuildRequest(instanceUrl: instanceUrl);

        await command.ExecuteAsync(request, CancellationToken.None);

        var instanceInDb = await db.OpenProjectInstances
            .FirstOrDefaultAsync(x => x.BaseUrl == instanceUrl);
        Assert.NotNull(instanceInDb);
    }
}
