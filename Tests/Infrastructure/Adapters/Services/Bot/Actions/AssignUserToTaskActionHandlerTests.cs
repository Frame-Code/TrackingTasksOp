using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class AssignUserToTaskActionHandlerTests
{
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageCommandMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();

    private AssignUserToTaskActionHandler BuildHandler() => new(
        _updateWorkPackageCommandMock.Object,
        _entityResolverMock.Object);

    [Fact]
    public async Task ExecuteAsync_AssigneeResolved_ShouldUpdateWorkPackage()
    {
        _entityResolverMock.Setup(r => r.ResolveUserId("yo", null)).ReturnsAsync(7);

        var action = new GroqAction
        {
            Action = "assign_user_to_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["assigneeName"] = "yo" }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("101", result);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(101, null, 7, null, null,
            UpdateWorkPackageCommandImpl.NoChange,
            UpdateWorkPackageCommandImpl.NoChange), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AssigneeNotFound_ShouldThrowAndNotUpdate()
    {
        _entityResolverMock.Setup(r => r.ResolveUserId("Pedro", null)).ReturnsAsync((int?)null);

        var action = new GroqAction
        {
            Action = "assign_user_to_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["assigneeName"] = "Pedro" }
        };

        var handler = BuildHandler();
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));

        Assert.Contains("Pedro", ex.Message);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ResponsibleNotFound_ShouldThrowAndNotUpdate()
    {
        _entityResolverMock.Setup(r => r.ResolveUserId("Pedro", null)).ReturnsAsync((int?)null);

        var action = new GroqAction
        {
            Action = "assign_user_to_task",
            Params = new Dictionary<string, object> { ["workPackageId"] = 101, ["responsibleName"] = "Pedro" }
        };

        var handler = BuildHandler();
        var ex = await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));

        Assert.Contains("Pedro", ex.Message);
        _updateWorkPackageCommandMock.Verify(c => c.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoWorkPackageId_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "assign_user_to_task",
            Params = new Dictionary<string, object> { ["assigneeName"] = "yo" }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }
}
