using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class ResumeTaskCommandImplTests
{
    private readonly Mock<ITaskRepository> _repositoryMock = new();
    private readonly Mock<IProjectRepository> _projectRepositoryMock = new();
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageCommandMock = new();
    private readonly Mock<IGetWorkPackageCommand> _getWorkPackageCommandMock = new();
    private readonly Mock<IProjectOpService> _projectOpServiceMock = new();

    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 2;
    }

    private ResumeTaskCommandImpl BuildUseCase() => new(
        _repositoryMock.Object,
        _projectRepositoryMock.Object,
        _updateWorkPackageCommandMock.Object,
        _getWorkPackageCommandMock.Object,
        _projectOpServiceMock.Object,
        new FakeCurrentUser());

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
    public async Task Execute_AlreadyHasActiveSession_ThrowsInvalidOperationException()
    {
        var detail = new TaskTimeDetail { StartTime = new DateTime(2026, 6, 1, 10, 0, 0) };
        var task = BuildTask(detail);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);

        var useCase = BuildUseCase();

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(new ResumeTaskRequest(1)));
    }

    [Fact]
    public async Task Execute_NoActiveSession_CreatesNewDetailAndUpdatesStatus()
    {
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 6, 1, 11, 0, 0)
        };
        var task = BuildTask(detail);
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        var result = await useCase.Execute(new ResumeTaskRequest(1, InProgressStatusId: 7));

        Assert.Equal(7, result.StatusTaskId);
        Assert.Equal(2, result.TasksTimeDetails.Count());
        var newDetail = result.TasksTimeDetails.OrderBy(x => x.StartTime).Last();
        Assert.Null(newDetail.EndTime);
        Assert.Equal("user-1", newDetail.UserId);
        _updateWorkPackageCommandMock.Verify(x => x.Execute(1, 7, null, null, null, UpdateWorkPackageCommandImpl.NoChange, UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task Execute_WithoutInProgressStatusId_DoesNotUpdateStatus()
    {
        var detail = new TaskTimeDetail
        {
            StartTime = new DateTime(2026, 6, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 6, 1, 11, 0, 0)
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 4;
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync(task);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        var useCase = BuildUseCase();
        await useCase.Execute(new ResumeTaskRequest(1));

        Assert.Equal(4, task.StatusTaskId);
        _updateWorkPackageCommandMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_TaskNotFoundLocally_CreatesFromOpenProjectAndStartsSession()
    {
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync((TaskEntity?)null);
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);

        _getWorkPackageCommandMock.Setup(x => x.Execute(1134)).ReturnsAsync(new WorkPackage
        {
            Id = 1134,
            Subject = "Prueba logs",
            Links = new WorkPackageLinks
            {
                Project = new() { Href = "/api/v3/projects/4" },
                Status = new() { Href = "/api/v3/statuses/3" }
            }
        });

        _projectRepositoryMock.Setup(x => x.GetByIdAsync(4, false)).ReturnsAsync((Project?)null);
        _projectOpServiceMock.Setup(x => x.Lists()).ReturnsAsync(new List<Domain.Entities.OpenProjectEntities.Project.Project>
        {
            new() { Id = 4, Name = "eProduction", Identifier = "eproduction", IsActive = true }
        });

        var useCase = BuildUseCase();
        var result = await useCase.Execute(new ResumeTaskRequest(1134, InProgressStatusId: 7));

        Assert.Equal(1134, result.WorkPackageId);
        Assert.Equal("Prueba logs", result.Name);
        Assert.Equal(4, result.ProjectId);
        Assert.Equal(7, result.StatusTaskId);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal(2, result.OpenProjectInstanceId);
        Assert.Single(result.TasksTimeDetails);
        Assert.Null(result.TasksTimeDetails.Single().EndTime);

        _projectRepositoryMock.Verify(x => x.SaveAsync(It.Is<Project>(p => p.Id == 4)), Times.Once);
        _updateWorkPackageCommandMock.Verify(x => x.Execute(1134, 7, null, null, null, UpdateWorkPackageCommandImpl.NoChange, UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task Execute_TaskNotFoundLocallyOrInOpenProject_ThrowsArgumentException()
    {
        _repositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<bool>())).ReturnsAsync((TaskEntity?)null);
        _getWorkPackageCommandMock.Setup(x => x.Execute(1134)).ReturnsAsync((WorkPackage?)null);

        var useCase = BuildUseCase();

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.Execute(new ResumeTaskRequest(1134)));
    }
}
