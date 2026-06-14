using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Project;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.OpenProjectEntities.User;
using Infrastructure.Adapters.Services.Bot;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class OpenProjectEntityResolverTests
{
    private readonly Mock<IProjectOpService> _projectOpServiceMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IUserOpService> _userOpServiceMock = new();

    private OpenProjectEntityResolver BuildResolver() => new(
        _projectOpServiceMock.Object,
        _statusOpServiceMock.Object,
        _userOpServiceMock.Object);

    [Fact]
    public async Task ResolveProjectId_ExactMatch_ShouldReturnId()
    {
        _projectOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Project { Id = 1, Name = "Tracking Tasks", Identifier = "tracking-tasks" },
            new Project { Id = 2, Name = "Other Project", Identifier = "other" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveProjectId("Tracking Tasks");

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ResolveProjectId_PartialMatch_ShouldReturnId()
    {
        _projectOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Project { Id = 1, Name = "Tracking Tasks Op", Identifier = "tracking-tasks-op" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveProjectId("Tracking");

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ResolveProjectId_NoMatch_ShouldReturnNull()
    {
        _projectOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Project { Id = 1, Name = "Tracking Tasks", Identifier = "tracking-tasks" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveProjectId("Inexistente");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveProjectId_EmptyName_ShouldReturnNullWithoutCallingService()
    {
        var resolver = BuildResolver();
        var result = await resolver.ResolveProjectId("");

        Assert.Null(result);
        _projectOpServiceMock.Verify(s => s.Lists(), Times.Never);
    }

    [Fact]
    public async Task ResolveStatusId_ExactMatch_ShouldReturnId()
    {
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 1, Name = "New" },
            new Status { Id = 2, Name = "In Progress" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveStatusId("New");

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ResolveStatusId_PartialMatch_ShouldReturnId()
    {
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 2, Name = "In Progress" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveStatusId("progress");

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ResolveStatusId_NoMatch_ShouldReturnNull()
    {
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 1, Name = "New" }
        ]);

        var resolver = BuildResolver();
        var result = await resolver.ResolveStatusId("Closed");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveUserId_UserFound_ShouldReturnId()
    {
        _userOpServiceMock.Setup(s => s.FindByName("Josué")).ReturnsAsync(new User { Id = 42, Name = "Josué" });

        var resolver = BuildResolver();
        var result = await resolver.ResolveUserId("Josué");

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ResolveUserId_UserNotFound_ShouldReturnNull()
    {
        _userOpServiceMock.Setup(s => s.FindByName("Inexistente")).ReturnsAsync((User?)null);

        var resolver = BuildResolver();
        var result = await resolver.ResolveUserId("Inexistente");

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveUserId_EmptyName_ShouldReturnNullWithoutCallingService()
    {
        var resolver = BuildResolver();
        var result = await resolver.ResolveUserId("");

        Assert.Null(result);
        _userOpServiceMock.Verify(s => s.FindByName(It.IsAny<string>()), Times.Never);
        _userOpServiceMock.Verify(s => s.FindAssigneeByName(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResolveUserId_WithProjectId_ShouldUseAvailableAssignees()
    {
        _userOpServiceMock.Setup(s => s.FindAssigneeByName(4, "Stin")).ReturnsAsync(new User { Id = 30, Name = "Stin Sanchez" });

        var resolver = BuildResolver();
        var result = await resolver.ResolveUserId("Stin", 4);

        Assert.Equal(30, result);
        _userOpServiceMock.Verify(s => s.FindByName(It.IsAny<string>()), Times.Never);
    }
}
