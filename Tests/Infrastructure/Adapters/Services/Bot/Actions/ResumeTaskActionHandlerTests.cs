using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class ResumeTaskActionHandlerTests
{
    private readonly Mock<IResumeTaskCommand> _resumeTaskCommandMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();

    private ResumeTaskActionHandler BuildHandler() => new(
        _resumeTaskCommandMock.Object,
        _entityResolverMock.Object);

    [Fact]
    public async Task ExecuteAsync_DefaultStatus_ShouldResolveInProgressAndResume()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId("In progress")).ReturnsAsync(7);
        _resumeTaskCommandMock.Setup(c => c.Execute(It.IsAny<ResumeTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 101, Name = "Task" });

        var action = new GroqAction
        {
            Action = "resume_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101 }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("101", result);
        _resumeTaskCommandMock.Verify(c => c.Execute(It.Is<ResumeTaskRequest>(r =>
            r.WorkPackageId == 101 && r.InProgressStatusId == 7)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CustomStatusName_ShouldResolveGivenStatus()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId("En curso")).ReturnsAsync(3);
        _resumeTaskCommandMock.Setup(c => c.Execute(It.IsAny<ResumeTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 101, Name = "Task" });

        var action = new GroqAction
        {
            Action = "resume_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["statusName"] = "En curso" }
        };

        var handler = BuildHandler();
        await handler.ExecuteAsync(action, null);

        _resumeTaskCommandMock.Verify(c => c.Execute(It.Is<ResumeTaskRequest>(r => r.InProgressStatusId == 3)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_UsesContextWpId()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(7);
        _resumeTaskCommandMock.Setup(c => c.Execute(It.IsAny<ResumeTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 303, Name = "Task" });

        var action = new GroqAction { Action = "resume_task", Params = new Dictionary<string, object>() };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, 303);

        Assert.Contains("303", result);
        _resumeTaskCommandMock.Verify(c => c.Execute(It.Is<ResumeTaskRequest>(r => r.WorkPackageId == 303)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_ShouldThrow()
    {
        var action = new GroqAction { Action = "resume_task", Params = new Dictionary<string, object>() };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }
}
