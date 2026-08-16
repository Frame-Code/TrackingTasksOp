using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Application.Dto.Conversation;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

/// <summary>
/// El tipo de work package se resolvía asumiendo el primero del proyecto, así que toda tarea
/// creada por el bot terminaba como "DESARROLLO". Ahora se infiere de lo que dijo el usuario
/// y, si no se puede, se le pregunta con los tipos reales del proyecto.
/// </summary>
public class WorkPackageTypeResolutionTests
{
    private static readonly List<WorkPackageType> ProjectTypes =
    [
        new(1, "DESARROLLO"),
        new(2, "IMPLEMENTACION"),
        new(3, "SOPORTE TECNICO"),
        new(4, "URGENTE"),
        new(5, "ERROR"),
        new(6, "GARANTIA")
    ];

    private readonly Mock<IStartTaskCommand> _startTaskCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();
    private readonly Mock<ICreateWorkPackageCommand> _createWorkPackageCommandMock = new();

    public WorkPackageTypeResolutionTests()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RequiredCustomField>());
        _createWorkPackageCommandMock.Setup(c => c.GetTypesAsync(It.IsAny<int>()))
            .ReturnsAsync(ProjectTypes);
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(7);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<Application.Dto.Tasks.StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 123, Name = "New Task", ProjectId = 10, StatusTaskId = 5 });
    }

    private CreateTaskActionHandler BuildHandler() => new(
        _startTaskCommandMock.Object,
        _statusOpServiceMock.Object,
        _entityResolverMock.Object,
        _createWorkPackageCommandMock.Object);

    private static GroqAction Action(string? typeName)
    {
        var p = new Dictionary<string, object>
        {
            ["projectName"] = "eProduction",
            ["statusName"] = "Nuevo",
            ["name"] = "Revisar nómina"
        };
        if (typeName is not null) p["typeName"] = typeName;
        return new GroqAction { Action = "create_task", Params = p };
    }

    [Theory]
    [InlineData("ERROR", 5)]
    [InlineData("error", 5)]
    [InlineData("soporte", 3)]
    [InlineData("SOPORTE TECNICO", 3)]
    [InlineData("garantia", 6)]
    public async Task ExecuteAsync_ConTipoReconocible_LoResuelveYCreaLaTarea(string typeName, int expectedTypeId)
    {
        var handler = BuildHandler();

        await handler.ExecuteAsync(Action(typeName), null, new ConversationContext());

        _startTaskCommandMock.Verify(c => c.Execute(It.Is<Application.Dto.Tasks.StarTaskRequest>(
            r => r.TypeId == expectedTypeId)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SinTipo_PreguntaConLosTiposDelProyectoYNoCrea()
    {
        var handler = BuildHandler();

        var message = await handler.ExecuteAsync(Action(null), null, new ConversationContext());

        Assert.Contains("SOPORTE TECNICO", message);
        Assert.Contains("GARANTIA", message);
        _startTaskCommandMock.Verify(c => c.Execute(It.IsAny<Application.Dto.Tasks.StarTaskRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ConTipoDesconocido_PreguntaEnVezDeAsumirElPrimero()
    {
        var handler = BuildHandler();

        var message = await handler.ExecuteAsync(Action("epica"), null, new ConversationContext());

        Assert.Contains("epica", message);
        _startTaskCommandMock.Verify(c => c.Execute(It.IsAny<Application.Dto.Tasks.StarTaskRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_TipoRespondidoEnSegundoTurno_UsaElBorradorYCrea()
    {
        var handler = BuildHandler();
        var context = new ConversationContext();

        // Turno 1: sin tipo -> queda el borrador con nombre y proyecto ya resueltos.
        await handler.ExecuteAsync(Action(null), null, context);
        Assert.NotNull(context.PendingStartTaskDraft);

        // Turno 2: el LLM solo manda el tipo; el resto sale del borrador persistido.
        var onlyType = new GroqAction
        {
            Action = "create_task",
            Params = new Dictionary<string, object> { ["projectName"] = "eProduction", ["typeName"] = "urgente" }
        };
        await handler.ExecuteAsync(onlyType, null, context);

        _startTaskCommandMock.Verify(c => c.Execute(It.Is<Application.Dto.Tasks.StarTaskRequest>(
            r => r.TypeId == 4 && r.Name == "Revisar nómina")), Times.Once);
    }

    [Fact]
    public void MatchType_NombreExacto_GanaSobreCoincidenciaParcial()
    {
        List<WorkPackageType> types = [new(1, "SOPORTE TECNICO EXTENDIDO"), new(2, "SOPORTE")];

        Assert.Equal(2, StartTaskRequestBuilder.MatchType(types, "SOPORTE")!.Id);
    }
}
