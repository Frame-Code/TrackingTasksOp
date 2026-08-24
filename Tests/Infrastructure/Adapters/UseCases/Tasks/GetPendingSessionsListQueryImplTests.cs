using Application.Ports.Auth;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class GetPendingSessionsListQueryImplTests
{
    private readonly Mock<ITaskRepository> _taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> _projectRepositoryMock = new();

    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    private GetPendingSessionsListQueryImpl BuildQuery() =>
        new(_taskRepositoryMock.Object, _projectRepositoryMock.Object, new FakeCurrentUser());

    private static TaskEntity BuildTask(int workPackageId, string name, int projectId, params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = workPackageId, UserId = "user-1", ProjectId = projectId, StatusTaskId = 1,
        Name = name, TasksTimeDetails = details.ToList()
    };

    private static TaskTimeDetail Closed(DateTime start, double hours, bool uploaded = false) => new()
    {
        StartTime = start, EndTime = start.AddHours(hours), Uploaded = uploaded
    };

    private void SetupProjects(params Project[] projects) =>
        _projectRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Project, bool>>>(), false))
            .ReturnsAsync(projects);

    [Fact]
    public async Task Execute_AgrupaPorTareaYExcluyeSubidas()
    {
        var task = BuildTask(42, "Tarea A", 1,
            Closed(new DateTime(2026, 8, 5, 9, 0, 0), 2),
            Closed(new DateTime(2026, 8, 6, 9, 0, 0), 1.5),
            Closed(new DateTime(2026, 8, 7, 9, 0, 0), 3, uploaded: true));
        _taskRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaskEntity, bool>>>(), false))
            .ReturnsAsync(new List<TaskEntity> { task });
        SetupProjects(new Project { Id = 1, Name = "Cliente X", OpenProjectInstanceId = 2 });

        var result = await BuildQuery().Execute();

        var row = Assert.Single(result);
        Assert.Equal(42, row.WorkPackageId);
        Assert.Equal("Tarea A", row.TaskName);
        Assert.Equal("Cliente X", row.ProjectName);
        Assert.Equal(3.5, row.Hours);
    }

    [Fact]
    public async Task Execute_ProyectoDesconocido_UsaFallback()
    {
        var task = BuildTask(1, "Tarea", 99, Closed(new DateTime(2026, 8, 5, 9, 0, 0), 1));
        _taskRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaskEntity, bool>>>(), false))
            .ReturnsAsync(new List<TaskEntity> { task });
        SetupProjects();

        var result = await BuildQuery().Execute();

        Assert.Equal("Desconocido", result.Single().ProjectName);
    }

    [Fact]
    public async Task Execute_SinSesionesPendientes_DevuelveListaVacia()
    {
        _taskRepositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaskEntity, bool>>>(), false))
            .ReturnsAsync(new List<TaskEntity>());
        SetupProjects();

        var result = await BuildQuery().Execute();

        Assert.Empty(result);
    }

    [Fact]
    public async Task Execute_UsuarioNoAutenticado_LanzaUnauthorizedAccessException()
    {
        var query = new GetPendingSessionsListQueryImpl(_taskRepositoryMock.Object, _projectRepositoryMock.Object, new NoUserCurrentUser());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => query.Execute());
    }

    private class NoUserCurrentUser : CurrentUser
    {
        public override string? UserId => null;
        public override bool IsAuthenticated => false;
        public override string? OpenProjectInstanceUrl => null;
        public override int? OpenProjectInstanceId => null;
        public override int? OpenProjectUserId => null;
    }
}
