using Application.Ports.Auth;
using Application.Ports.Repositories;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class GetPendingSessionsSummaryQueryImplTests
{
    private readonly Mock<ITaskRepository> _repositoryMock = new();

    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    private GetPendingSessionsSummaryQueryImpl BuildQuery() => new(_repositoryMock.Object, new FakeCurrentUser());

    private static TaskEntity BuildTask(params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = 42, UserId = "user-1", ProjectId = 1, StatusTaskId = 1,
        Name = "Task", TasksTimeDetails = details.ToList()
    };

    private static TaskTimeDetail Closed(DateTime start, double hours, bool uploaded = false) => new()
    {
        StartTime = start, EndTime = start.AddHours(hours), Uploaded = uploaded
    };

    [Fact]
    public async Task Execute_SumaHorasYCuentaSoloLasPendientes()
    {
        var task = BuildTask(
            Closed(new DateTime(2026, 8, 5, 9, 0, 0), 2),
            Closed(new DateTime(2026, 8, 6, 9, 0, 0), 1.5),
            Closed(new DateTime(2026, 8, 7, 9, 0, 0), 3, uploaded: true));
        _repositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaskEntity, bool>>>(), false))
            .ReturnsAsync(new List<TaskEntity> { task });

        var result = await BuildQuery().Execute();

        Assert.Equal(2, result.Count);
        Assert.Equal(3.5, result.TotalHours);
    }

    [Fact]
    public async Task Execute_SinSesionesPendientes_DevuelveCero()
    {
        _repositoryMock.Setup(x => x.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<TaskEntity, bool>>>(), false))
            .ReturnsAsync(new List<TaskEntity>());

        var result = await BuildQuery().Execute();

        Assert.Equal(0, result.Count);
        Assert.Equal(0, result.TotalHours);
    }

    [Fact]
    public async Task Execute_UsuarioNoAutenticado_LanzaUnauthorizedAccessException()
    {
        var query = new GetPendingSessionsSummaryQueryImpl(_repositoryMock.Object, new NoUserCurrentUser());

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
