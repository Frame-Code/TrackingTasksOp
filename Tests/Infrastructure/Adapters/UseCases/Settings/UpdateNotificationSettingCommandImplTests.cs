using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Infrastructure.Adapters.UseCases.Settings;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Settings;

public class UpdateNotificationSettingCommandImplTests
{
    private class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => 2;
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

    private static UpdateNotificationSettingCommandImpl BuildCommand(TrackingTasksDbContext db, CurrentUser currentUser) => new(db, currentUser);

    [Fact]
    public async Task Execute_UnknownTypeCode_ThrowsValidationException()
    {
        var db = BuildDbContext(nameof(Execute_UnknownTypeCode_ThrowsValidationException));
        var command = BuildCommand(db, new FakeCurrentUser("user-1"));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new UpdateNotificationSettingRequest("not-a-real-type", true, 15)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1441)]
    public async Task Execute_IntervalOutOfRange_ThrowsValidationException(int interval)
    {
        var db = BuildDbContext($"{nameof(Execute_IntervalOutOfRange_ThrowsValidationException)}-{interval}");
        var command = BuildCommand(db, new FakeCurrentUser("user-1"));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new UpdateNotificationSettingRequest(NotificationTypeCodes.SessionReminder, true, interval)));
    }

    [Fact]
    public async Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var db = BuildDbContext(nameof(Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException));
        var command = BuildCommand(db, new FakeCurrentUser(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            command.Execute(new UpdateNotificationSettingRequest(NotificationTypeCodes.SessionReminder, true, 15)));
    }

    [Fact]
    public async Task Execute_NoExistingRow_InsertsNewRow()
    {
        var db = BuildDbContext(nameof(Execute_NoExistingRow_InsertsNewRow));
        var command = BuildCommand(db, new FakeCurrentUser("user-1"));

        await command.Execute(new UpdateNotificationSettingRequest(NotificationTypeCodes.PendingUploadReminder, false, 45));

        var stored = await db.Set<UserNotificationSetting>()
            .SingleAsync(s => s.UserId == "user-1" && s.TypeCode == NotificationTypeCodes.PendingUploadReminder);
        Assert.False(stored.Enabled);
        Assert.Equal(45, stored.IntervalMinutes);
    }

    [Fact]
    public async Task Execute_ExistingRow_UpdatesValuesInPlace()
    {
        var db = BuildDbContext(nameof(Execute_ExistingRow_UpdatesValuesInPlace));
        db.Set<UserNotificationSetting>().Add(new UserNotificationSetting
        {
            UserId = "user-1", TypeCode = NotificationTypeCodes.SessionReminder, Enabled = true, IntervalMinutes = 15
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var command = BuildCommand(db, new FakeCurrentUser("user-1"));
        await command.Execute(new UpdateNotificationSettingRequest(NotificationTypeCodes.SessionReminder, false, 60));

        var stored = await db.Set<UserNotificationSetting>()
            .SingleAsync(s => s.UserId == "user-1" && s.TypeCode == NotificationTypeCodes.SessionReminder);
        Assert.False(stored.Enabled);
        Assert.Equal(60, stored.IntervalMinutes);
    }
}
