using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.Project;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Moq;
using Web.Infrastructure.Adapters.Services.Heuristics;
using Xunit;

namespace Tests.Infrastructure.Adapters.Services;

public class HeuristicHandlersTests
{
    private readonly Mock<IListsWorkPackagesCommand> _listsWorkPackagesMock;
    private readonly Mock<IStatusOpService> _statusOpMock;
    private readonly Mock<IProjectOpService> _projectOpMock;
    private readonly Mock<IGetWorkPackageCommand> _getWorkPackageMock;
    private readonly Mock<IAttachmentService> _attachmentMock;

    public HeuristicHandlersTests()
    {
        _listsWorkPackagesMock = new Mock<IListsWorkPackagesCommand>();
        _statusOpMock = new Mock<IStatusOpService>();
        _projectOpMock = new Mock<IProjectOpService>();
        _getWorkPackageMock = new Mock<IGetWorkPackageCommand>();
        _attachmentMock = new Mock<IAttachmentService>();

        _statusOpMock.Setup(x => x.Lists()).ReturnsAsync(new List<Status>());
        _projectOpMock.Setup(x => x.Lists()).ReturnsAsync(new List<Project>());
        _attachmentMock.Setup(x => x.GetAttachmentsAsync(It.IsAny<int>())).ReturnsAsync(new List<Attachment>());
    }

    [Fact]
    public async Task TaskQueryHandler_MisTareas_ExecutesCommandWithCorrectFilters()
    {
        var handler = new TaskQueryHandler(_listsWorkPackagesMock.Object, _statusOpMock.Object, _projectOpMock.Object);
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()))
            .ReturnsAsync(new List<WorkPackage>());

        var result = await handler.HandleAsync("mis tareas");

        Assert.Contains("No se encontraron tareas", result!);
        _listsWorkPackagesMock.Verify(x => x.Execute(It.Is<ListsWorkPackagesRequest>(r => r.AssigneeId == "me" && r.StatusOperator == "o")), Times.Once);
    }

    [Fact]
    public async Task TaskQueryHandler_TareasAbiertas_ExecutesCommandWithCorrectFilters()
    {
        var handler = new TaskQueryHandler(_listsWorkPackagesMock.Object, _statusOpMock.Object, _projectOpMock.Object);
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()))
            .ReturnsAsync(new List<WorkPackage>());

        var result = await handler.HandleAsync("tareas abiertas");

        _listsWorkPackagesMock.Verify(x => x.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusOperator == "o" && r.AssigneeId == null)), Times.Once);
    }

    [Fact]
    public async Task TaskQueryHandler_MisTareasEnEstado_CombinesFilters()
    {
        var handler = new TaskQueryHandler(_listsWorkPackagesMock.Object, _statusOpMock.Object, _projectOpMock.Object);
        var statusList = new List<Status> { new Status { Id = 1, Name = "New" } };
        _statusOpMock.Setup(x => x.Lists()).ReturnsAsync(statusList);
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()))
            .ReturnsAsync(new List<WorkPackage>());

        var result = await handler.HandleAsync("Lista mis tareas en estado new");

        _listsWorkPackagesMock.Verify(x => x.Execute(It.Is<ListsWorkPackagesRequest>(r => 
            r.AssigneeId == "me" && r.StatusId == 1 && r.StatusOperator == null)), Times.Once);
    }

    [Fact]
    public async Task TaskQueryHandler_ByStatus_FindsStatusAndFilters()
    {
        var handler = new TaskQueryHandler(_listsWorkPackagesMock.Object, _statusOpMock.Object, _projectOpMock.Object);
        var statusList = new List<Status> { new Status { Id = 10, Name = "En progreso" } };
        _statusOpMock.Setup(x => x.Lists()).ReturnsAsync(statusList);
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()))
            .ReturnsAsync(new List<WorkPackage>());

        var result = await handler.HandleAsync("tareas en estado 'En progreso'");

        _listsWorkPackagesMock.Verify(x => x.Execute(It.Is<ListsWorkPackagesRequest>(r => r.StatusId == 10 && r.StatusOperator == null)), Times.Once);
    }

    [Fact]
    public async Task TaskQueryHandler_ByProject_FindsProjectAndFilters()
    {
        var handler = new TaskQueryHandler(_listsWorkPackagesMock.Object, _statusOpMock.Object, _projectOpMock.Object);
        _projectOpMock.Setup(x => x.Lists()).ReturnsAsync(new List<Project> { new Project { Id = 5, Name = "Mi Proyecto" } });
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()))
            .ReturnsAsync(new List<WorkPackage>());

        var result = await handler.HandleAsync("tareas del proyecto Mi Proyecto");

        _listsWorkPackagesMock.Verify(x => x.Execute(It.Is<ListsWorkPackagesRequest>(r => r.ProjectId == 5 && r.StatusOperator == "o")), Times.Once);
    }

    [Fact]
    public async Task TaskDetailHandler_MatchesTaskId_ReturnsDetail()
    {
        var handler = new TaskDetailHandler(_getWorkPackageMock.Object, _attachmentMock.Object);
        var wp = new WorkPackage { Id = 45, Subject = "Tarea de Prueba" };
        _getWorkPackageMock.Setup(x => x.Execute(45)).ReturnsAsync(wp);

        var result = await handler.HandleAsync("detalle tarea 45");

        Assert.Contains("Tarea de Prueba", result!);
        Assert.Contains("#45", result!);
        _getWorkPackageMock.Verify(x => x.Execute(45), Times.Once);
    }

    [Fact]
    public async Task TaskDetailHandler_ComplexPrompt_MatchesTaskId()
    {
        var handler = new TaskDetailHandler(_getWorkPackageMock.Object, _attachmentMock.Object);
        var wp = new WorkPackage { Id = 50, Subject = "Tarea 50" };
        _getWorkPackageMock.Setup(x => x.Execute(50)).ReturnsAsync(wp);

        var result = await handler.HandleAsync("Detallame la tarea 50 del proyecto Test");

        Assert.Contains("Tarea 50", result!);
        Assert.Contains("#50", result!);
        _getWorkPackageMock.Verify(x => x.Execute(50), Times.Once);
    }

    [Fact]
    public async Task TaskDetailHandler_NoMatch_ReturnsNull()
    {
        var handler = new TaskDetailHandler(_getWorkPackageMock.Object, _attachmentMock.Object);

        var result = await handler.HandleAsync("hola mundo");

        Assert.Null(result);
        _getWorkPackageMock.Verify(x => x.Execute(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ListProjectsHandler_Matches_ReturnsProjects()
    {
        var handler = new ListProjectsHandler(_projectOpMock.Object);
        _projectOpMock.Setup(x => x.Lists()).ReturnsAsync(new List<Project> { new Project { Id = 1, Name = "P1" } });

        var result = await handler.HandleAsync("proyectos");

        Assert.Contains("P1", result!);
        _projectOpMock.Verify(x => x.Lists(), Times.Once);
    }

    [Fact]
    public async Task ListUsersHandler_Matches_ReturnsUsers()
    {
        var userMock = new Mock<IUserOpService>();
        userMock.Setup(x => x.Lists()).ReturnsAsync(new List<Domain.Entities.OpenProjectEntities.User.User> { new() { Id = 1, Name = "User1" } });
        var handler = new ListUsersHandler(userMock.Object);

        var result = await handler.HandleAsync("listar usuarios");

        Assert.Contains("User1", result!);
        userMock.Verify(x => x.Lists(), Times.Once);
    }

    [Fact]
    public async Task ListStatusesHandler_Matches_ReturnsStatuses()
    {
        var handler = new ListStatusesHandler(_statusOpMock.Object);
        _statusOpMock.Setup(x => x.Lists()).ReturnsAsync(new List<Status> { new Status { Id = 1, Name = "Status1" } });

        var result = await handler.HandleAsync("estados");

        Assert.Contains("Status1", result!);
        _statusOpMock.Verify(x => x.Lists(), Times.Once);
    }
}
