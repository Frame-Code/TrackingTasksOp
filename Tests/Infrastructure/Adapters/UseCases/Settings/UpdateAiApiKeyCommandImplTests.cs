using Application.Dto.Auth;
using Application.Ports.Auth;
using Infrastructure.Adapters.UseCases.Settings;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Settings;

public class UpdateAiApiKeyCommandImplTests
{
    private class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = BuildUserManagerMock();
    private readonly Mock<IApiKeyEncryptorService> _encryptorMock = new();

    private UpdateAiApiKeyCommandImpl BuildCommand(CurrentUser currentUser) =>
        new(_userManagerMock.Object, _encryptorMock.Object, currentUser);

    [Fact]
    public async Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var command = BuildCommand(new FakeCurrentUser(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            command.Execute(new UpdateAiApiKeyRequest("some-key")));
    }

    [Fact]
    public async Task Execute_ConApiKey_LaCifraYLaGuarda()
    {
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userManagerMock.Setup(x => x.UpdateAsync(appUser)).ReturnsAsync(IdentityResult.Success);
        _encryptorMock.Setup(x => x.Protect("my-groq-key")).Returns("cipher-text");

        var command = BuildCommand(new FakeCurrentUser("user-1"));
        await command.Execute(new UpdateAiApiKeyRequest("my-groq-key"));

        Assert.Equal("cipher-text", appUser.EncryptedGroqApiKey);
        _userManagerMock.Verify(x => x.UpdateAsync(appUser), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_SinApiKey_QuitaLaKeyPropia(string? apiKey)
    {
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com", EncryptedGroqApiKey = "old-cipher" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userManagerMock.Setup(x => x.UpdateAsync(appUser)).ReturnsAsync(IdentityResult.Success);

        var command = BuildCommand(new FakeCurrentUser("user-1"));
        await command.Execute(new UpdateAiApiKeyRequest(apiKey));

        Assert.Null(appUser.EncryptedGroqApiKey);
        _encryptorMock.Verify(x => x.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UpdateFails_ThrowsApplicationExceptionWithErrors()
    {
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userManagerMock.Setup(x => x.UpdateAsync(appUser))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }));

        var command = BuildCommand(new FakeCurrentUser("user-1"));

        var ex = await Assert.ThrowsAsync<ApplicationException>(() =>
            command.Execute(new UpdateAiApiKeyRequest("key")));
        Assert.Contains("boom", ex.Message);
    }
}
