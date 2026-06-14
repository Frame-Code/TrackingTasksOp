using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class PauseTaskActionHandlerTests
{
    private readonly Mock<IPauseTaskCommand> _pauseTaskCommandMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();

    private PauseTaskActionHandler BuildHandler() => new(
        _pauseTaskCommandMock.Object,
        _entityResolverMock.Object);

    [Fact]
    public async Task ExecuteAsync_DefaultStatus_ShouldResolveOnHoldAndPause()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId("On hold")).ReturnsAsync(9);
        _pauseTaskCommandMock.Setup(c => c.Execute(It.IsAny<PauseTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 101, Name = "Task" });

        var action = new GroqAction
        {
            Action = "pause_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101 }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("101", result);
        _pauseTaskCommandMock.Verify(c => c.Execute(It.Is<PauseTaskRequest>(r =>
            r.WorkPackageId == 101 && r.OnHoldStatusId == 9)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CustomStatusName_ShouldResolveGivenStatus()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId("En espera")).ReturnsAsync(12);
        _pauseTaskCommandMock.Setup(c => c.Execute(It.IsAny<PauseTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 101, Name = "Task" });

        var action = new GroqAction
        {
            Action = "pause_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["statusName"] = "En espera" }
        };

        var handler = BuildHandler();
        await handler.ExecuteAsync(action, null);

        _pauseTaskCommandMock.Verify(c => c.Execute(It.Is<PauseTaskRequest>(r => r.OnHoldStatusId == 12)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_UsesContextWpId()
    {
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(9);
        _pauseTaskCommandMock.Setup(c => c.Execute(It.IsAny<PauseTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 303, Name = "Task" });

        var action = new GroqAction { Action = "pause_task", Params = new Dictionary<string, object>() };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, 303);

        Assert.Contains("303", result);
        _pauseTaskCommandMock.Verify(c => c.Execute(It.Is<PauseTaskRequest>(r => r.WorkPackageId == 303)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_ShouldThrow()
    {
        var action = new GroqAction { Action = "pause_task", Params = new Dictionary<string, object>() };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }
}
