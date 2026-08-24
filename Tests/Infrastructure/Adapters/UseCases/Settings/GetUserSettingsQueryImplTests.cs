using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.Services;
using Infrastructure.Adapters.UseCases.Settings;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.DataAccess.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Settings;

public class GetUserSettingsQueryImplTests
{
    private class FakeCurrentUser(string? userId, string? instanceUrl) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => instanceUrl;
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    // NoTracking a propósito: replica el comportamiento real (DbContextExtensions.AddDbContext()).
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

    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock = BuildUserManagerMock();
    private readonly Mock<IUserOpService> _userOpServiceMock = new();

    private GetUserSettingsQueryImpl BuildQuery(TrackingTasksDbContext db, CurrentUser currentUser) => new(
        db, _userManagerMock.Object, _userOpServiceMock.Object, currentUser);

    [Fact]
    public async Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var db = BuildDbContext(nameof(Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException));
        var query = BuildQuery(db, new FakeCurrentUser(null, null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => query.Execute());
    }

    [Fact]
    public async Task Execute_NoSavedRows_ReturnsDefaultsForEveryKnownType()
    {
        var db = BuildDbContext(nameof(Execute_NoSavedRows_ReturnsDefaultsForEveryKnownType));
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com" };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);

        var result = await BuildQuery(db, new FakeCurrentUser("user-1", "http://op.example.com")).Execute();

        Assert.Equal(NotificationTypeCodes.All.Length, result.Notifications.Count);
        Assert.All(result.Notifications, n =>
        {
            Assert.True(n.Enabled);
            Assert.Equal(NotificationTypeCodes.DefaultIntervalMinutes, n.IntervalMinutes);
        });
        Assert.Equal("Ask", result.PauseDefaultBehavior);
        Assert.False(result.SkipCancelConfirmation);
        Assert.True(result.AddRandomSlackTime);
        Assert.Empty(result.DefaultStatusIds);
        Assert.False(result.HasCustomAiApiKey);
    }

    [Fact]
    public async Task Execute_OneTypeOverridden_MergesSavedRowWithDefaultForTheOther()
    {
        var db = BuildDbContext(nameof(Execute_OneTypeOverridden_MergesSavedRowWithDefaultForTheOther));
        var appUser = new ApplicationUser
        {
            Id = "user-1", Email = "user@test.com",
            PauseDefaultBehavior = PauseDefaultBehavior.SaveLocal, SkipCancelConfirmation = true,
            AddRandomSlackTime = false, EncryptedGroqApiKey = "cipher-text",
            DefaultStatusFilterIds = "1,3"
        };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);

        db.Set<UserNotificationSetting>().Add(new UserNotificationSetting
        {
            UserId = "user-1", TypeCode = NotificationTypeCodes.PendingUploadReminder,
            Enabled = false, IntervalMinutes = 30
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await BuildQuery(db, new FakeCurrentUser("user-1", "http://op.example.com")).Execute();

        var pending = result.Notifications.Single(n => n.TypeCode == NotificationTypeCodes.PendingUploadReminder);
        Assert.False(pending.Enabled);
        Assert.Equal(30, pending.IntervalMinutes);

        var session = result.Notifications.Single(n => n.TypeCode == NotificationTypeCodes.SessionReminder);
        Assert.True(session.Enabled);
        Assert.Equal(15, session.IntervalMinutes);

        Assert.Equal("SaveLocal", result.PauseDefaultBehavior);
        Assert.True(result.SkipCancelConfirmation);
        Assert.False(result.AddRandomSlackTime);
        Assert.Equal([1, 3], result.DefaultStatusIds);
        Assert.True(result.HasCustomAiApiKey);
    }

    [Fact]
    public async Task Execute_UserIsAdminInOpenProject_ReturnsIsAdminTrue()
    {
        var db = BuildDbContext(nameof(Execute_UserIsAdminInOpenProject_ReturnsIsAdminTrue));
        var appUser = new ApplicationUser { Id = "user-1", Email = "user@test.com", OpenProjectUserId = 7 };
        _userManagerMock.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(appUser);
        _userOpServiceMock.Setup(x => x.IsAdmin(7)).ReturnsAsync(true);

        var result = await BuildQuery(db, new FakeCurrentUser("user-1", "http://op.example.com")).Execute();

        Assert.True(result.IsAdmin);
    }
}
