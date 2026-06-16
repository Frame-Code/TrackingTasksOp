using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.Status;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services.Bot.Actions;

public class StartTaskActionHandlerTests
{
    private readonly Mock<IStartTaskCommand> _startTaskCommandMock = new();
    private readonly Mock<IStatusOpService> _statusOpServiceMock = new();
    private readonly Mock<IOpenProjectEntityResolver> _entityResolverMock = new();
    private readonly Mock<ICreateWorkPackageCommand> _createWorkPackageCommandMock = new();

    public StartTaskActionHandlerTests()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<RequiredCustomField>());
    }

    private StartTaskActionHandler BuildHandler() => new(
        _startTaskCommandMock.Object,
        _statusOpServiceMock.Object,
        _entityResolverMock.Object,
        _createWorkPackageCommandMock.Object);

    [Fact]
    public async Task ExecuteAsync_WithProjectAndStatusNames_ShouldResolveIdsAndExecute()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId("MyProject")).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId("In Progress")).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync((int?)null);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["statusName"] = "In Progress",
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 202", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.ProjectId == 10 &&
            r.StatusId == 5 &&
            r.Name == "New Task")), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NewTaskWithoutAssignee_ShouldWarnTaskWontAppearInMyTasks()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync((int?)null);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["statusName"] = "In Progress",
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 202", result);
        Assert.Contains("sin asignado", result);
        Assert.Contains("Mis tareas", result);
    }

    [Fact]
    public async Task ExecuteAsync_NewTaskWithAssignee_ShouldNotWarn()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId("Stin", It.IsAny<int?>())).ReturnsAsync(30);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["statusName"] = "In Progress",
                ["name"] = "New Task",
                ["assigneeName"] = "Stin"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 202", result);
        Assert.DoesNotContain("sin asignado", result);
    }

    [Fact]
    public async Task ExecuteAsync_WithStartAndDueDate_ShouldPassDatesToCommand()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId("Stin", It.IsAny<int?>())).ReturnsAsync(30);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["statusName"] = "In Progress",
                ["name"] = "New Task",
                ["assigneeName"] = "Stin",
                ["startDate"] = "2026-06-13",
                ["dueDate"] = "2026-06-13"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 202", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.StartDate == new DateOnly(2026, 6, 13) &&
            r.DueDate == new DateOnly(2026, 6, 13))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutDates_ShouldNotSetDates()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId("Stin", It.IsAny<int?>())).ReturnsAsync(30);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["statusName"] = "In Progress",
                ["name"] = "New Task",
                ["assigneeName"] = "Stin"
            }
        };

        var handler = BuildHandler();
        await handler.ExecuteAsync(action, null);

        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.StartDate == null &&
            r.DueDate == null)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithProjectAndStatusIds_ShouldExecuteWithoutResolving()
    {
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 202, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 202", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r => r.Name == "New Task" && r.ProjectId == 1 && r.StatusId == 1)), Times.Once);
        _entityResolverMock.Verify(r => r.ResolveProjectId(It.IsAny<string>()), Times.Never);
        _entityResolverMock.Verify(r => r.ResolveStatusId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoStatusGiven_ShouldFallbackToFirstAvailableStatus()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _statusOpServiceMock.Setup(s => s.Lists()).ReturnsAsync([
            new Status { Id = 3, Name = "Nuevo" },
            new Status { Id = 4, Name = "Closed" }
        ]);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 303, Name = "New Task" });

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "MyProject",
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 303", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r => r.StatusId == 3)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MissingTaskName_ShouldThrow()
    {
        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object> { ["projectId"] = 1, ["statusId"] = 1 }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }

    [Fact]
    public async Task ExecuteAsync_ProjectNameNotResolved_ShouldThrow()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId("Inexistente")).ReturnsAsync((int?)null);

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "Inexistente",
                ["statusId"] = 1,
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        await Assert.ThrowsAsync<Exception>(() => handler.ExecuteAsync(action, null));
    }

    [Fact]
    public async Task ExecuteAsync_RequiredCustomFieldsMissing_ShouldAskUserWithoutCreatingTask()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(1, null)).ReturnsAsync(
        [
            new RequiredCustomField("customField3", "Area", "CustomOption",
            [
                new CustomFieldOption(7, "PRODUCCION"),
                new CustomFieldOption(8, "ADMINISTRACION")
            ]),
            new RequiredCustomField("customField5", "Modulo", "CustomOption",
            [
                new CustomFieldOption(11, "Backend"),
                new CustomFieldOption(12, "Frontend")
            ])
        ]);

        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task"
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("Area", result);
        Assert.Contains("PRODUCCION", result);
        Assert.Contains("Modulo", result);
        Assert.Contains("Backend", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.IsAny<StarTaskRequest>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_RequiredCustomFieldsProvided_ShouldResolveOptionIdsAndExecute()
    {
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(1, null)).ReturnsAsync(
        [
            new RequiredCustomField("customField5", "Modulo", "CustomOption",
            [
                new CustomFieldOption(11, "Backend"),
                new CustomFieldOption(12, "Frontend")
            ])
        ]);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 404, Name = "New Task" });

        var customFieldsJson = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Modulo"] = "Backend" });
        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task",
                ["customFields"] = customFieldsJson
            }
        };

        var handler = BuildHandler();
        var result = await handler.ExecuteAsync(action, null);

        Assert.Contains("ID: 404", result);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.CustomFieldOptionIds != null &&
            r.CustomFieldOptionIds["customField5"] == 11)), Times.Once);
    }
}
