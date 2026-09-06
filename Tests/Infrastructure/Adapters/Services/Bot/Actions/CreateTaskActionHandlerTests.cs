using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
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

        // Sin tipos configurados el builder no pregunta por el tipo, que es lo que asumen
        // estos casos. Los tests de resolución de tipo sobrescriben este setup.
        _createWorkPackageCommandMock.Setup(c => c.GetTypesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<WorkPackageType>());
        // Sin "statusName" el builder cae al estado por defecto ("nuevo" o el primero),
        // que es justo el camino de "creá una subtarea dentro de la #412".
        _statusOpServiceMock.Setup(s => s.Lists())
            .ReturnsAsync([new Domain.Entities.OpenProjectEntities.Status.Status { Id = 5, Name = "Nuevo" }]);
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

    // ── Subtareas ─────────────────────────────────────────────────────────────────────

    private static GroqAction SubtaskAction(Dictionary<string, object> extraParams)
    {
        var p = new Dictionary<string, object> { ["name"] = "Acta de firma" };
        foreach (var (k, v) in extraParams) p[k] = v;
        return new GroqAction { Action = "create_task", Params = p };
    }

    [Fact]
    public async Task ExecuteAsync_ConParentId_HeredaElProyectoDelPadre()
    {
        // "creá una subtarea dentro de la #412 llamada Acta de firma" tiene que alcanzar:
        // sin esto el bot repregunta el proyecto, que ya se puede deducir del padre.
        _entityResolverMock.Setup(r => r.GetProjectIdOfWorkPackage(412)).ReturnsAsync(77);

        StarTaskRequest? captured = null;
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .Callback<StarTaskRequest>(r => captured = r)
            .ReturnsAsync(new TaskEntity { WorkPackageId = 432, Name = "Acta de firma" });

        var message = await BuildHandler().ExecuteAsync(SubtaskAction(new() { ["parentId"] = 412 }), null);

        Assert.Equal(412, captured!.ParentId);
        Assert.Equal(77, captured.ProjectId);
        Assert.Contains("#412", message);
        _entityResolverMock.Verify(r => r.ResolveProjectId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ConParentName_UnicaCoincidencia_LaUsaComoPadre()
    {
        _entityResolverMock.Setup(r => r.FindWorkPackagesBySubject("Levantamiento de datos"))
            .ReturnsAsync([new WorkPackage { Id = 418, Subject = "Levantamiento de datos" }]);
        _entityResolverMock.Setup(r => r.GetProjectIdOfWorkPackage(418)).ReturnsAsync(77);

        StarTaskRequest? captured = null;
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .Callback<StarTaskRequest>(r => captured = r)
            .ReturnsAsync(new TaskEntity { WorkPackageId = 432, Name = "Acta de firma" });

        await BuildHandler().ExecuteAsync(SubtaskAction(new() { ["parentName"] = "Levantamiento de datos" }), null);

        Assert.Equal(418, captured!.ParentId);
    }

    [Fact]
    public async Task ExecuteAsync_ConParentName_VariasCoincidencias_PreguntaSinCrear()
    {
        _entityResolverMock.Setup(r => r.FindWorkPackagesBySubject("Levantamiento"))
            .ReturnsAsync([
                new WorkPackage { Id = 418, Subject = "Levantamiento de datos" },
                new WorkPackage { Id = 501, Subject = "Levantamiento de requerimientos" }
            ]);

        var message = await BuildHandler().ExecuteAsync(SubtaskAction(new() { ["parentName"] = "Levantamiento" }), null);

        Assert.Contains("#418", message);
        Assert.Contains("#501", message);
        _startTaskCommandMock.Verify(c => c.Execute(It.IsAny<StarTaskRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ConParentName_SinCoincidencias_Falla()
    {
        _entityResolverMock.Setup(r => r.FindWorkPackagesBySubject(It.IsAny<string>()))
            .ReturnsAsync([]);

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            BuildHandler().ExecuteAsync(SubtaskAction(new() { ["parentName"] = "Inexistente" }), null));

        Assert.Contains("Inexistente", ex.Message);
        _startTaskCommandMock.Verify(c => c.Execute(It.IsAny<StarTaskRequest>()), Times.Never);
    }
}
