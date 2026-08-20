using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Infrastructure.Adapters.UseCases.Settings;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Settings;

public class UpdateTaskPreferencesCommandImplTests
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

    private UpdateTaskPreferencesCommandImpl BuildCommand(CurrentUser currentUser) => new(_userManagerMock.Object, currentUser);

    [Fact]
    public async Task Execute_InvalidPauseBehavior_ThrowsValidationException()
    {
        var command = BuildCommand(new FakeCurrentUser("user-1"));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new UpdateTaskPreferencesRequest("NotARealValue", false, true, [])));
    }

    [Fact]
    public async Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var command = BuildCommand(new FakeCurrentUser(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            command.Execute(new UpdateTaskPreferencesRequest("Ask", false, true, [])));
    }

    [Fact]
    public async Task Execute_HappyPath_UpdatesApplicationUserFields()
    {
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userManagerMock.Setup(x => x.UpdateAsync(appUser)).ReturnsAsync(IdentityResult.Success);

        var command = BuildCommand(new FakeCurrentUser("user-1"));
        await command.Execute(new UpdateTaskPreferencesRequest("UploadNow", true, false, [1, 3]));

        Assert.Equal(PauseDefaultBehavior.UploadNow, appUser.PauseDefaultBehavior);
        Assert.True(appUser.SkipCancelConfirmation);
        Assert.False(appUser.AddRandomSlackTime);
        Assert.Equal("1,3", appUser.DefaultStatusFilterIds);
        _userManagerMock.Verify(x => x.UpdateAsync(appUser), Times.Once);
    }

    [Fact]
    public async Task Execute_EmptyDefaultStatusIds_ClearsSavedFilter()
    {
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com", DefaultStatusFilterIds = "1,2" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userManagerMock.Setup(x => x.UpdateAsync(appUser)).ReturnsAsync(IdentityResult.Success);

        var command = BuildCommand(new FakeCurrentUser("user-1"));
        await command.Execute(new UpdateTaskPreferencesRequest("Ask", false, true, []));

        Assert.Null(appUser.DefaultStatusFilterIds);
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
            command.Execute(new UpdateTaskPreferencesRequest("Ask", false, true, [])));
        Assert.Contains("boom", ex.Message);
    }
}
