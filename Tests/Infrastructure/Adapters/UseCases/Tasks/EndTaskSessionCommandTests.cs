using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.Activity;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Exceptions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandTests
{
    private static (EndTaskSessionCommandImpl useCase,
                    Mock<ITaskRepository> repositoryMock,
                    Mock<IAddTimeEntryCommand> addTimeEntryMock,
                    Mock<IUpdateWorkPackageCommand> updateMock,
                    Mock<IActivityOpService> activityOpServiceMock)
        BuildUseCase(TaskEntity? taskFromRepo)
    {
        var repositoryMock = new Mock<ITaskRepository>();
        repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(taskFromRepo);

        repositoryMock
            .Setup(x => x.SaveAsync(It.IsAny<TaskEntity>()))
            .ReturnsAsync((TaskEntity t) => t);

        var addTimeEntryMock = new Mock<IAddTimeEntryCommand>();
        addTimeEntryMock
            .Setup(x => x.Execute(It.IsAny<AddTimeEntryRequest>()))
            .Returns(Task.CompletedTask);

        var updateMock = new Mock<IUpdateWorkPackageCommand>();
        updateMock
            .Setup(x => x.Execute(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var activityOpServiceMock = new Mock<IActivityOpService>();
        activityOpServiceMock
            .Setup(x => x.Lists(It.IsAny<int>()))
            .ReturnsAsync(new List<ActivityAllowedValue> { new() { Id = 99, Name = "Development" } });

        var useCase = new EndTaskSessionCommandImpl(
            repositoryMock.Object,
            addTimeEntryMock.Object,
            updateMock.Object,
            activityOpServiceMock.Object);

        return (useCase, repositoryMock, addTimeEntryMock, updateMock, activityOpServiceMock);
    }

    private static TaskEntity BuildTask(params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = 1,
        ProjectId = 1,
        StatusTaskId = 1,
        Name = "Create user module",
        Description = "desc",
        TasksTimeDetails = details.ToList()
    };

    [Fact]
    public async Task Execute_TaskNotFound_ThrowsArgumentException()
    {
        var (useCase, _, _, _, _) = BuildUseCase(null);
        var request = new EndTaskSessionRequest(99, 2, "no existe");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => useCase.Execute(request));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Execute_TaskWithoutTimeDetails_ThrowsInvalidOperationException()
    {
        var task = BuildTask();
        var (useCase, _, _, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "sin sesiones");

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(request));
    }

    [Fact]
    public async Task Execute_LastDetailAlreadyClosed_CallsAddTimeEntryAndSaves()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 12, 30, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, addMock, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "done");

        var result = await useCase.Execute(request);

        Assert.NotNull(result);
        Assert.True(detail.Uploaded);
        Assert.Equal(new DateTime(2026, 5, 1, 12, 30, 0), detail.EndTime);
        addMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r =>
            r.IdWorkPackage == 1 &&
            r.IdActivity == 2 &&
            r.Comment == "done" &&
            r.Hours == 2.5)), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.Once);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_LastDetailWithoutEndTime_SetsEndTimeAndUploads()
    {
        // StartTime muy reciente para que (DateTime.Now - StartTime).Minutes sea 0
        // y no entre a las ramas con TimeTrackService.GetRandomMinutes (no determinista).
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = DateTime.Now,
            EndTime = null,
            Uploaded = false
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, addMock, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "auto-cierre");

        await useCase.Execute(request);

        Assert.NotNull(detail.EndTime);
        Assert.True(detail.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.Once);
    }

    [Fact]
    public async Task Execute_WithNewStatusId_UpdatesWorkPackageAndTask()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        await useCase.Execute(request);

        Assert.Equal(5, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            1, 5, null, null, null, It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Execute_StatusUpdateFails_ThrowsTaskStatusTransitionExceptionAfterPersistingTimeEntry()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, repoMock, addMock, updateMock, _) = BuildUseCase(task);
        updateMock
            .Setup(x => x.Execute(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Error HTTP 422: {\"message\":\"Estado no es válido porque no existe ninguna transición.\"}"));
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        var ex = await Assert.ThrowsAsync<TaskStatusTransitionException>(() => useCase.Execute(request));

        Assert.Contains("Estado no es válido porque no existe ninguna transición.", ex.Message);
        Assert.Equal(1, task.StatusTaskId);
        Assert.True(detail.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.Once);
    }

    [Fact]
    public async Task Execute_InvalidStatusTransition_IncludesAllowedStatusesSuggestion()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        updateMock
            .Setup(x => x.Execute(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidStatusTransitionException(
                "Estado no es válido porque no existe ninguna transición.",
                new List<string> { "New", "In progress", "Closed" }));
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        var ex = await Assert.ThrowsAsync<TaskStatusTransitionException>(() => useCase.Execute(request));

        Assert.Contains("Estado no es válido porque no existe ninguna transición.", ex.Message);
        Assert.Contains("Desde el estado actual puedes cambiar directamente a: New, In progress, Closed.", ex.Message);
    }

    [Fact]
    public async Task Execute_WithoutNewStatusId_DoesNotCallUpdateWorkPackage()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 7;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "sin cambiar status");

        await useCase.Execute(request);

        Assert.Equal(7, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithNewStatusIdZero_DoesNotCallUpdateWorkPackage()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 7;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "status invalido", NewStatusId: 0);

        await useCase.Execute(request);

        Assert.Equal(7, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_PicksLatestTimeDetailByStartTime()
    {
        var oldest = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 4, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 10, 0, 0),
            Uploaded = false
        };
        var middle = new TaskTimeDetail
        {
            Id = 2,
            StartTime = new DateTime(2026, 4, 15, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 15, 10, 0, 0),
            Uploaded = false
        };
        var latest = new TaskTimeDetail
        {
            Id = 3,
            StartTime = new DateTime(2026, 5, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 10, 30, 0),
            Uploaded = false
        };
        // Orden mezclado a propósito: la lógica debe tomar el de StartTime más reciente.
        var task = BuildTask(middle, oldest, latest);
        var (useCase, _, addMock, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "ultima sesion");

        await useCase.Execute(request);

        Assert.False(oldest.Uploaded);
        Assert.False(middle.Uploaded);
        Assert.True(latest.Uploaded);
        addMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r => r.Hours == 1.5)), Times.Once);
    }
}
