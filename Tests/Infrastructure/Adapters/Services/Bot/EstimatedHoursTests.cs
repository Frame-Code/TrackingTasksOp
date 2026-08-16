using System.Text.Json;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Application.Dto.Conversation;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Estimación de trabajo ("Trabajo" en OpenProject) indicada desde el prompt.
/// Es opcional: si el usuario no la menciona, la tarea se crea sin estimar.
/// </summary>
public class EstimatedHoursTests
{
    [Theory]
    [InlineData(1, "PT1H")]
    [InlineData(1.5, "PT1H30M")]
    [InlineData(0.5, "PT30M")]
    [InlineData(0.75, "PT45M")]
    [InlineData(8, "PT8H")]
    [InlineData(2.25, "PT2H15M")]
    public void ToIso8601Duration_ConvierteHorasDecimalesAlFormatoDeOpenProject(double hours, string expected)
    {
        Assert.Equal(expected, CreateWorkPackageCommandImpl.ToIso8601Duration(hours));
    }

    [Fact]
    public void ToIso8601Duration_RedondeaAlMinutoMasCercano()
    {
        // 1/3 de hora = 20 minutos exactos; 0.999h no debe producir "PT0H59.94M".
        Assert.Equal("PT20M", CreateWorkPackageCommandImpl.ToIso8601Duration(1.0 / 3));
        Assert.Equal("PT1H", CreateWorkPackageCommandImpl.ToIso8601Duration(0.999));
    }

    private static Dictionary<string, object> Params(string json)
        => JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

    [Fact]
    public void GetNullableDouble_AceptaNumeroYTexto()
    {
        Assert.Equal(1.5, GroqActionParams.GetNullableDouble(Params("""{"estimatedHours": 1.5}"""), "estimatedHours"));
        Assert.Equal(1.5, GroqActionParams.GetNullableDouble(Params("""{"estimatedHours": "1.5"}"""), "estimatedHours"));
        // El LLM puede responder con coma decimal si el usuario escribe en español.
        Assert.Equal(1.5, GroqActionParams.GetNullableDouble(Params("""{"estimatedHours": "1,5"}"""), "estimatedHours"));
    }

    [Fact]
    public void GetNullableDouble_DevuelveNullSiFaltaONoEsInterpretable()
    {
        Assert.Null(GroqActionParams.GetNullableDouble(Params("""{"name": "x"}"""), "estimatedHours"));
        Assert.Null(GroqActionParams.GetNullableDouble(Params("""{"estimatedHours": "un rato"}"""), "estimatedHours"));
        Assert.Null(GroqActionParams.GetNullableDouble(null, "estimatedHours"));
    }

    // ── Recorrido completo: del prompt al request ────────────────────────────
    private readonly Mock<IStartTaskCommand> _startTaskCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();
    private readonly Mock<ICreateWorkPackageCommand> _createWorkPackageCommandMock = new();

    public EstimatedHoursTests()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RequiredCustomField>());
        _createWorkPackageCommandMock.Setup(c => c.GetTypesAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<WorkPackageType> { new(5, "ERROR") });
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<Application.Dto.Tasks.StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 1, Name = "T", ProjectId = 10, StatusTaskId = 5 });
    }

    private CreateTaskActionHandler BuildHandler() => new(
        _startTaskCommandMock.Object, _statusOpServiceMock.Object,
        _entityResolverMock.Object, _createWorkPackageCommandMock.Object);

    private static GroqAction Action(Dictionary<string, object> extra)
    {
        var p = new Dictionary<string, object>
        {
            ["projectName"] = "eProduction",
            ["statusName"] = "Nuevo",
            ["name"] = "Arreglar nómina",
            ["typeName"] = "ERROR"
        };
        foreach (var (k, v) in extra) p[k] = v;
        return new GroqAction { Action = "create_task", Params = p };
    }

    [Fact]
    public async Task ExecuteAsync_ConEstimacion_LaPasaAlRequest()
    {
        await BuildHandler().ExecuteAsync(
            Action(Params("""{"estimatedHours": 1.5}""")), null, new ConversationContext());

        _startTaskCommandMock.Verify(c => c.Execute(It.Is<Application.Dto.Tasks.StarTaskRequest>(
            r => r.EstimatedHours == 1.5)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SinEstimacion_CreaLaTareaIgualSinEstimar()
    {
        await BuildHandler().ExecuteAsync(
            Action([]), null, new ConversationContext());

        _startTaskCommandMock.Verify(c => c.Execute(It.Is<Application.Dto.Tasks.StarTaskRequest>(
            r => r.EstimatedHours == null)), Times.Once);
    }
}
