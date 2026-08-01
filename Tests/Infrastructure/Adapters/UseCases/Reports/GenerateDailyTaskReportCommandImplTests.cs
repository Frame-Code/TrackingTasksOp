using System.Linq.Expressions;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Infrastructure.Adapters.UseCases.Reports;
using Moq;
using Task = Domain.Entities.TrackingTasksEntities.Task;
using TaskTimeDetail = Domain.Entities.TrackingTasksEntities.TaskTimeDetail;

namespace Tests.Infrastructure.Adapters.UseCases.Reports;

public class GenerateDailyTaskReportCommandImplTests
{
    private static Task BuildTask(int workPackageId, string name, int projectId, int statusId, params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = workPackageId,
        UserId = "user-1",
        Name = name,
        ProjectId = projectId,
        StatusTaskId = statusId,
        TasksTimeDetails = details.ToList()
    };

    private static readonly Dictionary<int, string> ProjectNames = new() { [1] = "eProduction", [2] = "Otro Proyecto" };
    private static readonly Dictionary<int, string> StatusNames = new() { [1] = "In Progress", [2] = "Closed" };

    [Fact]
    public void BuildReportRows_MultipleSessionsSameTaskSameDay_SumsHours()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) },
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 11, 0, 0), EndTime = new DateTime(2026, 8, 3, 12, 30, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 8, 3), rows[0].Date);
        Assert.Equal(3.5, rows[0].Hours);
        Assert.Equal("eProduction", rows[0].ProjectName);
        Assert.Equal(101, rows[0].WorkPackageId);
        Assert.Equal("In Progress", rows[0].StatusName);
    }

    [Fact]
    public void BuildReportRows_OpenSession_IsExcluded()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = null });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildReportRows_SessionOutsideRange_IsExcluded()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 7, 15, 8, 0, 0), EndTime = new DateTime(2026, 7, 15, 10, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildReportRows_MultipleTasksAndDays_SortsByDateThenProjectThenName()
    {
        var taskB = BuildTask(102, "Tarea B", 2, 2,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 2, 9, 0, 0), EndTime = new DateTime(2026, 8, 2, 10, 0, 0) });
        var taskA = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 9, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [taskB, taskA], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new DateOnly(2026, 8, 2), rows[0].Date);
        Assert.Equal(new DateOnly(2026, 8, 3), rows[1].Date);
    }

    [Fact]
    public void BuildReportRows_UnknownProjectOrStatus_FallsBackToDesconocido()
    {
        var task = BuildTask(101, "Tarea A", 99, 99,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 9, 0, 0) });

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [task], new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), ProjectNames, StatusNames);

        Assert.Equal("Desconocido", rows[0].ProjectName);
        Assert.Equal("Desconocido", rows[0].StatusName);
    }

    [Fact]
    public void BuildWorkbook_WritesHeadersRowsAndTotal()
    {
        var rows = new List<DailyTaskReportRow>
        {
            new(new DateOnly(2026, 8, 3), "eProduction", 101, "Tarea A", "In Progress", 3.5),
            new(new DateOnly(2026, 8, 4), "eProduction", 102, "Tarea B", "Closed", 2.0)
        };

        var bytes = GenerateDailyTaskReportCommandImpl.BuildWorkbook(rows);

        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Fecha", ws.Cell(1, 1).GetString());
        Assert.Equal("Proyecto", ws.Cell(1, 2).GetString());
        Assert.Equal("ID Tarea", ws.Cell(1, 3).GetString());
        Assert.Equal("Nombre", ws.Cell(1, 4).GetString());
        Assert.Equal("Estado", ws.Cell(1, 5).GetString());
        Assert.Equal("Horas", ws.Cell(1, 6).GetString());

        Assert.Equal("2026-08-03", ws.Cell(2, 1).GetString());
        Assert.Equal("eProduction", ws.Cell(2, 2).GetString());
        Assert.Equal(101, ws.Cell(2, 3).GetValue<int>());
        Assert.Equal("Tarea A", ws.Cell(2, 4).GetString());
        Assert.Equal("In Progress", ws.Cell(2, 5).GetString());
        Assert.Equal(3.5, ws.Cell(2, 6).GetValue<double>());

        Assert.Equal("Total", ws.Cell(4, 5).GetString());
        Assert.Equal(5.5, ws.Cell(4, 6).GetValue<double>());
    }

    [Fact]
    public void BuildWorkbook_NoRows_WritesHeadersOnlyWithZeroTotal()
    {
        var bytes = GenerateDailyTaskReportCommandImpl.BuildWorkbook([]);

        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Fecha", ws.Cell(1, 1).GetString());
        Assert.Equal("Total", ws.Cell(2, 5).GetString());
        Assert.Equal(0d, ws.Cell(2, 6).GetValue<double>());
    }

    private class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 1;
        public override int? OpenProjectUserId => 1;
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_FromAfterTo_ThrowsValidationException()
    {
        var repoMock = new Mock<ITaskRepository>();
        var projectRepoMock = new Mock<IProjectRepository>();
        var statusRepoMock = new Mock<IStatusTaskRepository>();
        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser("user-1"));

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => command.Execute(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_NoAuthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var repoMock = new Mock<ITaskRepository>();
        var projectRepoMock = new Mock<IProjectRepository>();
        var statusRepoMock = new Mock<IStatusTaskRepository>();
        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser(null));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => command.Execute(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10)));
    }

    [Fact]
    public async System.Threading.Tasks.Task Execute_HappyPath_ReturnsWorkbookWithResolvedNames()
    {
        var task = BuildTask(101, "Tarea A", 1, 1,
            new TaskTimeDetail { StartTime = new DateTime(2026, 8, 3, 8, 0, 0), EndTime = new DateTime(2026, 8, 3, 10, 0, 0) });

        var repoMock = new Mock<ITaskRepository>();
        repoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Task, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([task]);

        var projectRepoMock = new Mock<IProjectRepository>();
        projectRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Domain.Entities.TrackingTasksEntities.Project, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([new Domain.Entities.TrackingTasksEntities.Project { Id = 1, Name = "eProduction" }]);

        var statusRepoMock = new Mock<IStatusTaskRepository>();
        statusRepoMock.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<Domain.Entities.TrackingTasksEntities.StatusTask, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([new Domain.Entities.TrackingTasksEntities.StatusTask { Id = 1, Name = "In Progress" }]);

        var command = new GenerateDailyTaskReportCommandImpl(
            repoMock.Object, projectRepoMock.Object, statusRepoMock.Object, new FakeCurrentUser("user-1"));

        var bytes = await command.Execute(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        Assert.NotEmpty(bytes);
        using var ms = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);
        var ws = workbook.Worksheet(1);
        Assert.Equal("eProduction", ws.Cell(2, 2).GetString());
        Assert.Equal("Tarea A", ws.Cell(2, 4).GetString());
        Assert.Equal("In Progress", ws.Cell(2, 5).GetString());

        repoMock.Verify(r => r.GetAllAsync(It.IsAny<Expression<Func<Task, bool>>>(), It.IsAny<bool>()), Times.Once);
    }
}
