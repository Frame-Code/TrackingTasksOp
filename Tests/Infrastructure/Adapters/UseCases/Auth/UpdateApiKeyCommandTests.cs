using Application.Dto.Auth;
using Application.Ports.Auth;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;
using OpUser = Domain.Entities.OpenProjectEntities.User.User;

namespace Tests.Infrastructure.Adapters.UseCases.Auth;

public class UpdateApiKeyCommandTests
{
    private class FakeCurrentUser(string? userId, string? instanceUrl, int? instanceId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => instanceUrl;
        public override int? OpenProjectInstanceId => instanceId;
        public override int? OpenProjectUserId => 7;
    }

    // NoTracking a propósito: replica DbContextExtensions.AddDbContext() (comportamiento real en
    // producción). Sin esto, este test no hubiera detectado que SaveChangesAsync() no persistía
    // los cambios por faltar un Update() explícito.
    private static TrackingTasksDbContext BuildDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
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

    private readonly Mock<IApiKeyValidatorService> _validatorMock = new();
    private readonly Mock<IApiKeyEncryptorService> _encryptorMock = new();
    private readonly Mock<IAuthAuditLogger> _auditLoggerMock = new();
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = BuildUserManagerMock();

    private UpdateApiKeyCommandImpl BuildCommand(TrackingTasksDbContext db, CurrentUser currentUser) => new(
        _validatorMock.Object,
        _encryptorMock.Object,
        _auditLoggerMock.Object,
        new Mock<ILogger<UpdateApiKeyCommandImpl>>().Object,
        db,
        _userManagerMock.Object,
        currentUser);

    [Fact]
    public async Task ExecuteAsync_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_NoAuthenticatedUser_ThrowsUnauthorizedAccessException));
        var command = BuildCommand(db, new FakeCurrentUser(null, null, null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            command.ExecuteAsync(new UpdateApiKeyRequest { ApiKey = "new-key" }, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NoOpenProjectInstance_ThrowsInvalidOperationException()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_NoOpenProjectInstance_ThrowsInvalidOperationException));
        var command = BuildCommand(db, new FakeCurrentUser("user-1", null, null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            command.ExecuteAsync(new UpdateApiKeyRequest { ApiKey = "new-key" }, CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_NoLocalCredentialForUser_ReturnsFailureWithoutValidating()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_NoLocalCredentialForUser_ReturnsFailureWithoutValidating));
        var command = BuildCommand(db, new FakeCurrentUser("user-1", "http://op.example.com", 1));

        var result = await command.ExecuteAsync(new UpdateApiKeyRequest { ApiKey = "new-key" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("No se encontró una credencial local para este usuario.", result.ErrorMessage);
        _validatorMock.Verify(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidApiKey_ThrowsAndDoesNotOverwriteExistingCredential()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_InvalidApiKey_ThrowsAndDoesNotOverwriteExistingCredential));
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com", OpenProjectInstanceBaseUrl = "http://op.example.com" };
        var credential = new LocalCredential
        {
            UserId = "user-1",
            ApplicationUser = appUser,
            EncryptedApiKey = "old-cipher",
            ApiKeyStatus = ApiKeyStatus.Valid,
            ApiKeyLastValidatedAt = new DateTime(2026, 1, 1)
        };
        await db.Users.AddAsync(appUser);
        await db.Set<LocalCredential>().AddAsync(credential);
        await db.SaveChangesAsync();
        // Simula que la siembra pasó en un DbContext previo (otro request): sin esto, la
        // entidad de la siembra queda trackeada y choca con la que ExecuteAsync vuelve a
        // traer con la misma key, algo que no puede pasar en producción (context por request).
        db.ChangeTracker.Clear();

        _validatorMock
            .Setup(v => v.ValidateAsync("http://op.example.com", "bad-key", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidApiKeyException("API key inválida o sin permisos"));

        var command = BuildCommand(db, new FakeCurrentUser("user-1", "http://op.example.com", 1));

        await Assert.ThrowsAsync<InvalidApiKeyException>(() =>
            command.ExecuteAsync(new UpdateApiKeyRequest { ApiKey = "bad-key" }, CancellationToken.None));

        var stored = await db.Set<LocalCredential>().FirstAsync(x => x.UserId == "user-1");
        Assert.Equal("old-cipher", stored.EncryptedApiKey);
    }

    [Fact]
    public async Task ExecuteAsync_HappyPath_UpdatesCredentialAndReturnsSuccess()
    {
        var db = BuildDbContext(nameof(ExecuteAsync_HappyPath_UpdatesCredentialAndReturnsSuccess));
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com", UserName = "user@test.com", OpenProjectInstanceBaseUrl = "http://op.example.com" };
        var credential = new LocalCredential
        {
            UserId = "user-1",
            ApplicationUser = appUser,
            EncryptedApiKey = "old-cipher",
            ApiKeyStatus = ApiKeyStatus.Valid,
            ApiKeyLastValidatedAt = new DateTime(2026, 1, 1)
        };
        await db.Users.AddAsync(appUser);
        await db.Set<LocalCredential>().AddAsync(credential);
        await db.SaveChangesAsync();
        // Simula que la siembra pasó en un DbContext previo (otro request): sin esto, la
        // entidad de la siembra queda trackeada y choca con la que ExecuteAsync vuelve a
        // traer con la misma key, algo que no puede pasar en producción (context por request).
        db.ChangeTracker.Clear();

        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _validatorMock
            .Setup(v => v.ValidateAsync("http://op.example.com", "new-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpUser { Id = 7, Name = "Stin Sanchez", Email = "user@test.com" });
        _encryptorMock.Setup(e => e.Protect("new-key")).Returns("new-cipher");

        var command = BuildCommand(db, new FakeCurrentUser("user-1", "http://op.example.com", 1));

        var result = await command.ExecuteAsync(new UpdateApiKeyRequest { ApiKey = "new-key" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("user-1", result.Data.UserId);
        Assert.Equal("Stin Sanchez", result.Data.Name);
        Assert.Equal("http://op.example.com", result.Data.OpenProjectInstanceUrl);

        var stored = await db.Set<LocalCredential>().FirstAsync(x => x.UserId == "user-1");
        Assert.Equal("new-cipher", stored.EncryptedApiKey);
        Assert.Equal(ApiKeyStatus.Valid, stored.ApiKeyStatus);

        _auditLoggerMock.Verify(x => x.LogAsync(
            AuditEventType.ApiKeyChanged,
            "user-1",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
