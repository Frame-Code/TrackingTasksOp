using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class ListTasksActionHandlerTests
{
    private readonly Mock<IListsWorkPackagesCommand> _listsWorkPackagesCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();

    private ListTasksActionHandler BuildHandler() => new(
        _listsWorkPackagesCommandMock.Object,
        _statusOpServiceMock.Object,
        _entityResolverMock.Object);

    private static GroqAction ActionWithStatus(string? statusName) => new()
    {
        Action = "list_tasks",
        Params = statusName == null ? null : new Dictionary<string, object> { ["statusName"] = statusName }
    };

    [Fact]
    public async Task ExecuteAsync_StatusResolves_AndHasResults_ShouldReturnFilteredTasks()
    {
        // statusName resuelve y OpenProject devuelve resultados -> lista formateada con esos work packages
        _entityResolverMock.Setup(r => r.ResolveStatusId("New")).ReturnsAsync(7);
        _listsWorkPackagesCommandMock.Setup(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == 7)))
            .ReturnsAsync([
                new WorkPackage { Id = 101, Subject = "Tarea A", Links = new WorkPackageLinks { Project = new LinkObject { Title = "Proyecto X" }, Status = new LinkObject { Title = "New" } } }
            ]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithStatus("New"), null);

        Assert.Contains("Tarea A", result);
        Assert.Contains("#101", result);
        Assert.Contains("New", result);
        _listsWorkPackagesCommandMock.Verify(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == 7)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StatusResolves_ButNoResults_ShouldReturnEmptyMessage()
    {
        // statusName resuelve y OpenProject devuelve vacío -> "No tienes tareas en estado..." (NO debe caer a "todas")
        _entityResolverMock.Setup(r => r.ResolveStatusId("New")).ReturnsAsync(7);
        _listsWorkPackagesCommandMock.Setup(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == 7)))
            .ReturnsAsync([]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithStatus("New"), null);

        Assert.Contains("No tienes tareas en estado", result);
        Assert.Contains("New", result);
        _listsWorkPackagesCommandMock.Verify(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == null)), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StatusDoesNotResolve_ShouldReturnAvailableStatuses()
    {
        // statusName NO resuelve (ej. "New" no existe) -> mensaje de error con estados disponibles (NO debe caer a "todas")
        _entityResolverMock.Setup(r => r.ResolveStatusId("Inexistente")).ReturnsAsync((int?)null);
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 1, Name = "New" },
            new Status { Id = 2, Name = "In Progress" }
        ]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithStatus("Inexistente"), null);

        Assert.Contains("No reconozco el estado", result);
        Assert.Contains("Inexistente", result);
        Assert.Contains("New", result);
        Assert.Contains("In Progress", result);
        _listsWorkPackagesCommandMock.Verify(c => c.Execute(It.IsAny<ListsWorkPackagesRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoStatusGiven_ShouldReturnAllPendingGroupedByProject()
    {
        // Sin statusName -> comportamiento actual (todas las pendientes agrupadas)
        _listsWorkPackagesCommandMock.Setup(c => c.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == null)))
            .ReturnsAsync([
                new WorkPackage { Id = 201, Subject = "Tarea B", Links = new WorkPackageLinks { Project = new LinkObject { Title = "Proyecto Y" }, Status = new LinkObject { Title = "Open" } } }
            ]);

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(ActionWithStatus(null), null);

        Assert.Contains("Tareas Pendientes", result);
        Assert.Contains("Proyecto Y", result);
        Assert.Contains("Tarea B", result);
        _entityResolverMock.Verify(r => r.ResolveStatusId(It.IsAny<string>()), Times.Never);
    }
}
