using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Exceptions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class StartTaskCommandImplTests
{
    private sealed class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 1;
        public override int? OpenProjectUserId => 7;
    }

    private static TaskEntity BuildTask(int wpId, params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = wpId,
        UserId = "user-1",
        Name = $"Tarea {wpId}",
        ProjectId = 1,
        StatusTaskId = 1,
        OpenProjectInstanceId = 1,
        TasksTimeDetails = details.ToList()
    };

    private static StartTaskCommandImpl BuildUseCase(TaskEntity? existingTask = null, TaskEntity? activeTask = null)
    {
        var repo = new Mock<ITaskRepository>();
        repo.Setup(r => r.GetByIdForUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(existingTask);
        repo.Setup(r => r.GetActiveByUserAsync(It.IsAny<string>())).ReturnsAsync(activeTask);
        repo.Setup(r => r.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.GetByIdForInstanceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(new Project { Id = 1, Name = "Proyecto", Identifier = "p", IsActive = true, OpenProjectInstanceId = 1 });

        return new StartTaskCommandImpl(
            repo.Object,
            projectRepo.Object,
            new Mock<IAddTimeEntryCommand>().Object,
            new Mock<ICreateWorkPackageCommand>().Object,
            new Mock<IProjectOpService>().Object,
            new FakeCurrentUser());
    }

    private static StarTaskRequest Request(int wpId, bool startTracking) => new()
    {
        WorkPackageId = wpId,
        Name = "Tarea",
        ProjectId = 1,
        StatusId = 1,
        StartTracking = startTracking
    };

    [Fact]
    public async Task Execute_SinStartTracking_NoCreaSesionDeTiempo()
    {
        var command = BuildUseCase(existingTask: BuildTask(1134));

        var result = await command.Execute(Request(1134, startTracking: false));

        Assert.Empty(result.TasksTimeDetails);
    }

    [Fact]
    public async Task Execute_ConStartTracking_CreaSesionDeTiempo()
    {
        var command = BuildUseCase(existingTask: BuildTask(1134));

        var result = await command.Execute(Request(1134, startTracking: true));

        var detail = Assert.Single(result.TasksTimeDetails);
        Assert.Null(detail.EndTime);
    }

    [Fact]
    public async Task Execute_ConOtraTareaCorriendo_LanzaConflicto()
    {
        var running = BuildTask(999, new TaskTimeDetail
        {
            UserId = "user-1", IdTask = 999, StartTime = DateTime.Now.AddHours(-2), EndTime = null
        });
        var command = BuildUseCase(existingTask: BuildTask(1134), activeTask: running);

        var ex = await Assert.ThrowsAsync<ActiveSessionConflictException>(
            () => command.Execute(Request(1134, startTracking: true)));

        Assert.Equal(999, ex.WorkPackageId);
    }

    [Fact]
    public async Task Execute_ConOtraTareaCorriendo_PeroSinTrackear_NoLanzaConflicto()
    {
        var running = BuildTask(999, new TaskTimeDetail
        {
            UserId = "user-1", IdTask = 999, StartTime = DateTime.Now.AddHours(-2), EndTime = null
        });
        var command = BuildUseCase(existingTask: BuildTask(1134), activeTask: running);

        var result = await command.Execute(Request(1134, startTracking: false));

        Assert.Empty(result.TasksTimeDetails);
    }

    [Fact]
    public async Task Execute_BuscaTareaYProyectoAcotadosAlTenantActual()
    {
        // Sin scope, dos tenants con el mismo WorkPackageId/ProjectId numérico podían
        // pisarse la tarea entre sí.
        var repo = new Mock<ITaskRepository>();
        repo.Setup(r => r.GetByIdForUserAsync(1134, "user-1", It.IsAny<bool>())).ReturnsAsync(BuildTask(1134));
        repo.Setup(r => r.GetActiveByUserAsync(It.IsAny<string>())).ReturnsAsync((TaskEntity?)null);
        repo.Setup(r => r.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.GetByIdForInstanceAsync(1, 1, It.IsAny<bool>()))
            .ReturnsAsync(new Project { Id = 1, Name = "Proyecto", Identifier = "p", IsActive = true, OpenProjectInstanceId = 1 });

        var command = new StartTaskCommandImpl(
            repo.Object, projectRepo.Object,
            new Mock<IAddTimeEntryCommand>().Object, new Mock<ICreateWorkPackageCommand>().Object,
            new Mock<IProjectOpService>().Object, new FakeCurrentUser());

        await command.Execute(Request(1134, startTracking: false));

        repo.Verify(r => r.GetByIdForUserAsync(1134, "user-1", It.IsAny<bool>()), Times.Once);
        projectRepo.Verify(r => r.GetByIdForInstanceAsync(1, 1, It.IsAny<bool>()), Times.Once);
    }
}
