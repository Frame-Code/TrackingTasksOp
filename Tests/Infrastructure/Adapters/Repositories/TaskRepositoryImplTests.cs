using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Repositories;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Repositories;

public class TaskRepositoryImplTests
{
    private static TrackingTasksDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrackingTasksDbContext(options);
    }

    private static TaskEntity BuildTask(int wpId, string userId, DateTime start, DateTime? end) => new()
    {
        WorkPackageId = wpId,
        UserId = userId,
        Name = $"Tarea {wpId}",
        ProjectId = 1,
        StatusTaskId = 1,
        OpenProjectInstanceId = 1,
        TasksTimeDetails = new List<TaskTimeDetail>
        {
            new() { UserId = userId, IdTask = wpId, StartTime = start, EndTime = end }
        }
    };

    [Fact]
    public async Task GetActiveByUserAsync_DevuelveLaTareaConSesionAbierta()
    {
        await using var context = BuildContext();
        context.Tasks.Add(BuildTask(100, "user-1", DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-2)));
        context.Tasks.Add(BuildTask(200, "user-1", DateTime.Now.AddHours(-1), null));
        await context.SaveChangesAsync();

        var repository = new TaskRepositoryImpl(context);

        var active = await repository.GetActiveByUserAsync("user-1");

        Assert.NotNull(active);
        Assert.Equal(200, active!.WorkPackageId);
    }

    [Fact]
    public async Task GetActiveByUserAsync_IgnoraSesionesDeOtroUsuario()
    {
        await using var context = BuildContext();
        context.Tasks.Add(BuildTask(300, "user-2", DateTime.Now.AddHours(-1), null));
        await context.SaveChangesAsync();

        var repository = new TaskRepositoryImpl(context);

        Assert.Null(await repository.GetActiveByUserAsync("user-1"));
    }

    [Fact]
    public async Task GetActiveByUserAsync_SinSesionesAbiertas_DevuelveNull()
    {
        await using var context = BuildContext();
        context.Tasks.Add(BuildTask(400, "user-1", DateTime.Now.AddHours(-3), DateTime.Now.AddHours(-2)));
        await context.SaveChangesAsync();

        var repository = new TaskRepositoryImpl(context);

        Assert.Null(await repository.GetActiveByUserAsync("user-1"));
    }
}
