using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.User;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class ListProjectUsersActionHandlerTests
{
    private readonly Mock<IUserOpService> _userOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();

    private ListProjectUsersActionHandler BuildHandler() => new(
        _userOpServiceMock.Object,
        _entityResolverMock.Object);

    private static GroqAction ActionWithProject(string? projectName) => new()
    {
        Action = "list_project_users",
        Params = projectName == null ? null : new Dictionary<string, object> { ["projectName"] = projectName }
    };

    [Fact]
    public async Task ExecuteAsync_ProjectResolves_ShouldReturnAssignees()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId("eProduction")).ReturnsAsync(4);
        _userOpServiceMock.Setup(s => s.ListAssignees(4)).ReturnsAsync(
        [
            new User { Id = 30, Name = "Stin Sanchez" },
            new User { Id = 31, Name = "Jefferson" }
        ]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithProject("eProduction"), null);

        Assert.Contains("Stin Sanchez", result);
        Assert.Contains("Jefferson", result);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectResolves_ButNoAssignees_ShouldReturnEmptyMessage()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId("eProduction")).ReturnsAsync(4);
        _userOpServiceMock.Setup(s => s.ListAssignees(4)).ReturnsAsync([]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithProject("eProduction"), null);

        Assert.Contains("No se encontraron usuarios", result);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectDoesNotResolve_ShouldThrow()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId("Inexistente")).ReturnsAsync((int?)null);

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(ActionWithProject("Inexistente"), null));
    }

    [Fact]
    public async Task ExecuteAsync_NoProjectName_ShouldThrow()
    {
        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(ActionWithProject(null), null));
    }
}
