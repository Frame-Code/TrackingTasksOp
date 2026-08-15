using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.Activity;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class PauseTaskCommandImplTests
{
    private readonly Mock<ITaskRepository> _repositoryMock = new();
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageCommandMock = new();
    private readonly Mock<IAddTimeEntryCommand> _addTimeEntryCommandMock = new();
    private readonly Mock<IActivityOpService> _activityOpServiceMock = new();

    public PauseTaskCommandImplTests()
    {
        _activityOpServiceMock
            .Setup(x => x.Lists(It.IsAny<int>()))
            .ReturnsAsync(new List<ActivityAllowedValue> { new() { Id = 99, Name = "Development" } });

        _addTimeEntryCommandMock
            .Setup(x => x.Execute(It.IsAny<AddTimeEntryRequest>()))
            .Returns(Task.CompletedTask);
    }

    // Se arma un PendingTimeUploaderImpl real sobre los mismos mocks: lo que interesa verificar
    // es qué entradas de tiempo llegan a OpenProject, no por qué colaborador pasan.
    private PauseTaskCommandImpl BuildUseCase() => new(
        _repositoryMock.Object,
        _updateWorkPackageCommandMock.Object,
        new PendingTimeUploaderImpl(
            _repositoryMock.Object,
            _addTimeEntryCommandMock.Object,
            _activityOpServiceMock.Object));

    private static TaskEntity BuildTask(params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = 1,
        UserId = "user-1",
        ProjectId = 1,
        StatusTaskId = 1,
        Name = "Task",
        TasksTimeDetails = details.ToList()
    };

    [Fact]
    public async Task Execute_TaskNotFound_ThrowsArgumentException()
    {
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync((TaskEntity?)null);

        var useCase = BuildUseCase();

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.Execute(new PauseTaskRequest(1)));
    }

    [Fact]
    public async Task Execute_NoActiveSession_ThrowsInvalidOperationException()
    {
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 6, 1, 11, 0, 0)
        };
        var task = BuildTask(detail);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);

        var useCase = BuildUseCase();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(new PauseTaskRequest(1)));
    }

    [Fact]
    public async Task Execute_WithActiveSession_ClosesAndUploadsDetail()
    {
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0)
        };
        var task = BuildTask(detail);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        var result = await useCase.Execute(new PauseTaskRequest(1, OnHoldStatusId: 9));

        Assert.NotNull(detail.EndTime);
        Assert.True(detail.Uploaded);
        Assert.Equal(9, result.StatusTaskId);
        _addTimeEntryCommandMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r =>
            r.IdWorkPackage == 1 && r.IdActivity == 99)), Times.Once);
        _updateWorkPackageCommandMock.Verify(x => x.Execute(1, 9, null, null, null, UpdateWorkPackageCommandImpl.NoChange, UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task Execute_WithoutOnHoldStatusId_DoesNotUpdateStatus()
    {
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0)
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 3;
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        await useCase.Execute(new PauseTaskRequest(1));

        Assert.Equal(3, task.StatusTaskId);
        _updateWorkPackageCommandMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithPreviousUnuploadedSession_UploadsBothSessions()
    {
        var previous = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var active = new TaskTimeDetail
        {
            Id = 2,
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0)
        };
        var task = BuildTask(previous, active);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        await useCase.Execute(new PauseTaskRequest(1));

        Assert.True(previous.Uploaded);
        Assert.True(active.Uploaded);
        _addTimeEntryCommandMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r => r.Hours == 2)), Times.Once);
        _addTimeEntryCommandMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_AlreadyUploadedSessions_AreNotUploadedAgain()
    {
        var previous = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = true
        };
        var active = new TaskTimeDetail
        {
            Id = 2,
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0)
        };
        var task = BuildTask(previous, active);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        await useCase.Execute(new PauseTaskRequest(1));

        Assert.True(active.Uploaded);
        _addTimeEntryCommandMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Once);
    }

    [Fact]
    public async Task Execute_ConUploadNowFalse_NoSubeNadaAOpenProject()
    {
        var active = new TaskTimeDetail { StartTime = new DateTime(2026, 6, 1, 10, 0, 0) };
        var task = BuildTask(active);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        var result = await useCase.Execute(new PauseTaskRequest(1, UploadNow: false));

        _addTimeEntryCommandMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Never);
        Assert.All(result.TasksTimeDetails, d => Assert.False(d.Uploaded));
    }

    [Fact]
    public async Task Execute_ConUploadNowFalse_CierraLaSesionConEndTime()
    {
        var active = new TaskTimeDetail { StartTime = new DateTime(2026, 6, 1, 10, 0, 0) };
        var task = BuildTask(active);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        var result = await useCase.Execute(new PauseTaskRequest(1, UploadNow: false));

        Assert.All(result.TasksTimeDetails, d => Assert.NotNull(d.EndTime));
    }
}
