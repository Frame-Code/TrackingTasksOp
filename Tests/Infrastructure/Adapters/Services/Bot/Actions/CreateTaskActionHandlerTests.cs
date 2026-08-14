using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class CreateTaskActionHandlerTests
{
    private readonly Mock<IStartTaskCommand> _startTaskCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();
    private readonly Mock<ICreateWorkPackageCommand> _createWorkPackageCommandMock = new();

    public CreateTaskActionHandlerTests()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RequiredCustomField>());
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync((int?)null);
    }

    private CreateTaskActionHandler BuildHandler() => new(
        _startTaskCommandMock.Object,
        _statusOpServiceMock.Object,
        _entityResolverMock.Object,
        _createWorkPackageCommandMock.Object);

    private static GroqAction Action() => new()
    {
        Action = "create_task",
        Params = new Dictionary<string, object>
        {
            ["projectName"] = "MyProject",
            ["statusName"] = "In Progress",
            ["name"] = "New Task"
        }
    };

    [Fact]
    public async Task ExecuteAsync_NoPideSeguimiento()
    {
        StarTaskRequest? captured = null;
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .Callback<StarTaskRequest>(r => captured = r)
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var handler = BuildHandler();
        var message = await handler.ExecuteAsync(Action(), null);

        Assert.NotNull(captured);
        Assert.False(captured!.StartTracking);
        Assert.Contains("creada", message);
        Assert.Contains("no", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SinAsignado_AdvierteQueNoApareceEnMisTareas()
    {
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var handler = BuildHandler();
        var message = await handler.ExecuteAsync(Action(), null);

        Assert.Contains("sin asignado", message);
        Assert.Contains("Mis tareas", message);
    }
}
