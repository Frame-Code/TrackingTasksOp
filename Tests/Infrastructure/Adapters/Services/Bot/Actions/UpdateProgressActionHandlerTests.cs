using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class UpdateProgressActionHandlerTests
{
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageCommandMock = new();

    private UpdateProgressActionHandler BuildHandler() => new(_updateWorkPackageCommandMock.Object);

    [Fact]
    public async Task ExecuteAsync_ValidProgress_ShouldUpdateAndReturnMessage()
    {
        var action = new GroqAction
        {
            Action = "update_progress",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["progress"] = 50 }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("101", result);
        Assert.Contains("50%", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(101, null, null, null, 50, UpdateWorkPackageCommandImpl.NoChange, UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_UsesContextWpId()
    {
        var action = new GroqAction
        {
            Action = "update_progress",
            Params = new Dictionary<string, object> { ["progress"] = 75 }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, 202);

        Assert.Contains("202", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(202, null, null, null, 75, UpdateWorkPackageCommandImpl.NoChange, UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "update_progress",
            Params = new Dictionary<string, object> { ["progress"] = 50 }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }

    [Fact]
    public async Task ExecuteAsync_NoProgress_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "update_progress",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101 }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ExecuteAsync_ProgressOutOfRange_ShouldThrow(int progress)
    {
        var action = new GroqAction
        {
            Action = "update_progress",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["progress"] = progress }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }
}
