using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class UpdateTaskDatesActionHandlerTests
{
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageCommandMock = new();

    private UpdateTaskDatesActionHandler BuildHandler() => new(_updateWorkPackageCommandMock.Object);

    [Fact]
    public async Task ExecuteAsync_WithStartAndDueDate_ShouldUpdateBoth()
    {
        var action = new GroqAction
        {
            Action = "update_task_dates",
            Params = new Dictionary<string, object>
            {
                ["workPackageId"] = 101,
                ["startDate"] = "2026-06-15",
                ["dueDate"] = "2026-06-20"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("101", result);
        Assert.Contains("2026-06-15", result);
        Assert.Contains("2026-06-20", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(
            101, null, null, null, null,
            "2026-06-15", "2026-06-20"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyStartDate_ShouldUpdateOnlyThat()
    {
        var action = new GroqAction
        {
            Action = "update_task_dates",
            Params = new Dictionary<string, object>
            {
                ["workPackageId"] = 101,
                ["startDate"] = "2026-06-15"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("2026-06-15", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(
            101, null, null, null, null,
            "2026-06-15", UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_UsesContextWpId()
    {
        var action = new GroqAction
        {
            Action = "update_task_dates",
            Params = new Dictionary<string, object> { ["dueDate"] = "2026-06-20" }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, 303);

        Assert.Contains("303", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(
            303, null, null, null, null,
            UpdateWorkPackageCommandImpl.NoChange, "2026-06-20"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "update_task_dates",
            Params = new Dictionary<string, object> { ["startDate"] = "2026-06-15" }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }

    [Fact]
    public async Task ExecuteAsync_NoDates_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "update_task_dates",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101 }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }
}
