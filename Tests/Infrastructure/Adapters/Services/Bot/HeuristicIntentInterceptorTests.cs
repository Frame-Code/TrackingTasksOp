using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.Project;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Adapters.Services.Bot;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class HeuristicIntentInterceptorTests
{
    private readonly Mock<IProjectOpService> _projectOpServiceMock = new();
    private readonly Mock<IListsWorkPackagesCommand> _listsWorkPackagesCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();

    private HeuristicIntentInterceptor BuildInterceptor() => new(
        _projectOpServiceMock.Object,
        _listsWorkPackagesCommandMock.Object,
        _statusOpServiceMock.Object);

    [Theory]
    [InlineData("Listar Proyectos")]
    [InlineData("¿Cuáles son mis proyectos?")]
    public void Normalize_ShouldStripAccentsAndLowercase(string input)
    {
        var interceptor = BuildInterceptor();
        var normalized = interceptor.Normalize(input);

        Assert.DoesNotContain("Á", normalized);
        Assert.Equal(normalized.ToLowerInvariant(), normalized);
    }

    [Fact]
    public async Task TryInterceptAsync_ListProjects_ShouldReturnFormattedProjects()
    {
        _projectOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Project { Id = 1, Name = "Tracking Tasks" }
        ]);

        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync("mis proyectos");

        Assert.NotNull(result);
        Assert.Contains("Tracking Tasks", result);
        Assert.Contains("ID: 1", result);
    }

    [Fact]
    public async Task TryInterceptAsync_PendingTasks_WithResults_ShouldReturnGroupedTasks()
    {
        _listsWorkPackagesCommandMock.Setup(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == null)))
            .ReturnsAsync([
                new WorkPackage { Id = 101, Subject = "Tarea A", Links = new WorkPackageLinks { Project = new LinkObject { Title = "Proyecto X" }, Status = new LinkObject { Title = "Open" } } }
            ]);

        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync("mis tareas pendientes");

        Assert.NotNull(result);
        Assert.Contains("Tareas Pendientes", result);
        Assert.Contains("Proyecto X", result);
        Assert.Contains("Tarea A", result);
    }

    [Fact]
    public async Task TryInterceptAsync_PendingTasks_NoResults_ShouldReturnCongrats()
    {
        _listsWorkPackagesCommandMock.Setup(c => c.Execute(It.IsAny<ListsWorkPackagesRequest>())).ReturnsAsync([]);

        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync("tareas pendientes");

        Assert.NotNull(result);
        Assert.Contains("Felicidades", result);
    }

    [Fact]
    public async Task TryInterceptAsync_ListStatuses_ShouldReturnFormattedStatuses()
    {
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 1, Name = "New" },
            new Status { Id = 2, Name = "Closed" }
        ]);

        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync("listar estados");

        Assert.NotNull(result);
        Assert.Contains("New", result);
        Assert.Contains("Closed", result);
    }

    [Fact]
    public async Task TryInterceptAsync_NoMatch_ShouldReturnNull()
    {
        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync("crea una tarea nueva en proyecto X");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("lista mis tareas en estado new")]
    [InlineData("dame mis tareas pendientes en estado en curso")]
    [InlineData("mis tareas del proyecto tracking")]
    public async Task TryInterceptAsync_PendingTasksWithFilterQualifier_ShouldDelegateToLlm(string prompt)
    {
        // Si el prompt menciona un calificador (estado/proyecto), no debe cortocircuitar
        // con la lista general de pendientes: debe dejar pasar al LLM para que resuelva el filtro.
        var interceptor = BuildInterceptor();
        var result = await interceptor.TryInterceptAsync(prompt);

        Assert.Null(result);
        _listsWorkPackagesCommandMock.Verify(c => c.Execute(It.IsAny<ListsWorkPackagesRequest>()), Times.Never);
    }
}
