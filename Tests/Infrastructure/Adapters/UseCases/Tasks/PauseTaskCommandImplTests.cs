using Application.Dto.Tasks;
using Application.Ports.Repositories;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
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

    private PauseTaskCommandImpl BuildUseCase() => new(
        _repositoryMock.Object,
        _updateWorkPackageCommandMock.Object);

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
    public async Task Execute_WithActiveSession_ClosesDetailWithoutUploading()
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
        Assert.False(detail.Uploaded);
        Assert.Equal(9, result.StatusTaskId);
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
}
