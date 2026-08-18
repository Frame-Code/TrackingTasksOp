using Application.Dto.ListWorkPackages;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.TimeEntries;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Infrastructure.Adapters.UseCases.Reports;
using Moq;
using DomainTask = Domain.Entities.TrackingTasksEntities.Task;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Reports;

/// <summary>RF4: el reporte debe poder acotarse al estado actual de la tarea.</summary>
public class ReportStatusFilterTests
{
    private sealed class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://localhost:8080";
        public override int? OpenProjectInstanceId => 1;
        public override int? OpenProjectUserId => 42;
    }

    private static OpTimeEntry Entry(int wpId) => new()
    {
        SpentOn = "2026-03-09",
        Hours = "PT1H",
        Links = new TimeEntryLinks
        {
            WorkPackage = new LinkObject { Href = $"/api/v3/work_packages/{wpId}", Title = $"Tarea {wpId}" },
            Project = new LinkObject { Href = "/api/v3/projects/5", Title = "eProduction" },
            Activity = new LinkObject { Href = "/api/v3/time_entries/activities/1", Title = "Development" }
        }
    };

    private static WorkPackage Wp(int id, int statusId, string statusName) => new()
    {
        Id = id,
        Subject = $"Tarea {id}",
        Links = new WorkPackageLinks
        {
            Status = new LinkObject { Href = $"/api/v3/statuses/{statusId}", Title = statusName },
            Type = new LinkObject { Href = "/api/v3/types/1", Title = "Tarea" }
        }
    };

    private static GenerateDailyTaskReportCommandImpl BuildCommand()
    {
        var timeEntries = new Mock<ITimeEntryOpService>();
        timeEntries.Setup(s => s.Lists(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<int?>()))
            .ReturnsAsync([Entry(101), Entry(202)]);

        var tasks = new Mock<ITaskRepository>();
        tasks.Setup(r => r.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<DomainTask, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var projects = new Mock<IProjectRepository>();
        projects.Setup(r => r.GetAllAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Domain.Entities.TrackingTasksEntities.Project, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync([]);

        var workPackages = new Mock<IListsWorkPackagesCommand>();
        workPackages.Setup(c => c.GetByIdsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Wp(101, 7, "Cerrada"), Wp(202, 1, "Nueva")]);

        return new GenerateDailyTaskReportCommandImpl(
            timeEntries.Object, tasks.Object, projects.Object, workPackages.Object, new FakeCurrentUser());
    }

    [Fact]
    public async Task Build_SinStatusId_DevuelveTodasLasTareas()
    {
        var data = await BuildCommand().Build(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(2, data.Rows.Count);
        Assert.Equal(2.0, data.TotalHours);
    }

    [Fact]
    public async Task Build_ConStatusId_SoloDevuelveLasTareasEnEseEstado()
    {
        var data = await BuildCommand().Build(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), statusId: 7);

        var row = Assert.Single(data.Rows);
        Assert.Equal(101, row.WorkPackageId);
        Assert.Equal("Cerrada", row.Status);
        Assert.Equal("Tarea", row.Type);
    }
}
