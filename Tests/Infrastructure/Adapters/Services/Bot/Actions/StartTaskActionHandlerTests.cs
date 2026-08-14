using Application.Dto.Conversation;
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
    public async Task ExecuteAsync_FuerzaStartTrackingTrue()
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
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r => r.StartTracking)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConflictoDeSesion_DevuelveMensajeConOpciones()
    {
        _entityResolverMock.Setup(r => r.ResolveProjectId(It.IsAny<string>())).ReturnsAsync(10);
        _entityResolverMock.Setup(r => r.ResolveStatusId(It.IsAny<string>())).ReturnsAsync(5);
        _entityResolverMock.Setup(r => r.ResolveUserId(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync((int?)null);
        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ThrowsAsync(new global::Infrastructure.Exceptions.ActiveSessionConflictException(999, "Otra tarea", DateTime.Now.AddHours(-1)));

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

        Assert.Contains("#999", result);
        Assert.Contains("Subirla ahora", result);
        Assert.Contains("Guardarla en local", result);
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

    [Fact]
    public async Task ExecuteAsync_AlreadyProvidedFieldsAcrossTurns_ShouldNotBeAskedAgain()
    {
        // Reproduce el bug reportado: el usuario da "Area" en el primer turno, falta "Modulo".
        // En el segundo turno solo da "Modulo" (como cualquier conversación real, no repite
        // datos ya dados) y la tarea debe crearse usando el "Area" recordado, no volver a pedirlo.
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

        var conversationContext = new ConversationContext { SessionId = "session1" };
        var handler = BuildHandler();

        var firstTurnFields = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Area"] = "PRODUCCION" });
        var firstAction = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task",
                ["customFields"] = firstTurnFields
            }
        };

        var firstResult = await handler.ExecuteAsync(firstAction, null, conversationContext);

        Assert.Contains("Modulo", firstResult);
        Assert.DoesNotContain("Area", firstResult);
        Assert.Equal(1, conversationContext.PendingTaskProjectId);
        Assert.Equal("PRODUCCION", conversationContext.PendingCustomFields?["Area"]);

        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 505, Name = "New Task" });

        var secondTurnFields = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Modulo"] = "Backend" });
        var secondAction = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task",
                ["customFields"] = secondTurnFields
            }
        };

        var secondResult = await handler.ExecuteAsync(secondAction, null, conversationContext);

        Assert.Contains("ID: 505", secondResult);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.CustomFieldOptionIds != null &&
            r.CustomFieldOptionIds["customField3"] == 7 &&
            r.CustomFieldOptionIds["customField5"] == 11)), Times.Once);
        Assert.Null(conversationContext.PendingCustomFields);
        Assert.Null(conversationContext.PendingTaskProjectId);
    }

    [Fact]
    public async Task ExecuteAsync_SecondTurnOmitsCoreFields_ShouldUsePersistedDraft()
    {
        // El modelo (LLM chico) no siempre repite project/name/assignee/fechas al responder
        // una pregunta de "falta este dato". El handler debe poder completar la tarea igual,
        // usando el borrador guardado en el turno anterior.
        _entityResolverMock.Setup(r => r.ResolveUserId("Stin", It.IsAny<int?>())).ReturnsAsync(30);
        _createWorkPackageCommandMock.Setup(c => c.GetRequiredCustomFieldsAsync(1, null)).ReturnsAsync(
        [
            new RequiredCustomField("customField3", "Area", "CustomOption",
            [
                new CustomFieldOption(7, "PRODUCCION"),
                new CustomFieldOption(8, "ADMINISTRACION")
            ])
        ]);

        var conversationContext = new ConversationContext { SessionId = "session1" };
        var handler = BuildHandler();

        var firstAction = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectId"] = 1,
                ["statusId"] = 1,
                ["name"] = "New Task",
                ["assigneeName"] = "Stin",
                ["startDate"] = "2026-06-13"
            }
        };

        var firstResult = await handler.ExecuteAsync(firstAction, null, conversationContext);

        Assert.Contains("Area", firstResult);
        Assert.NotNull(conversationContext.PendingStartTaskDraft);
        Assert.Equal("New Task", conversationContext.PendingStartTaskDraft!.Name);
        Assert.Equal(1, conversationContext.PendingStartTaskDraft.StatusId);
        Assert.Equal(30, conversationContext.PendingStartTaskDraft.AssigneeId);
        Assert.Equal(new DateOnly(2026, 6, 13), conversationContext.PendingStartTaskDraft.StartDate);

        _startTaskCommandMock.Setup(c => c.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new TaskEntity { WorkPackageId = 606, Name = "New Task" });

        var secondTurnFields = System.Text.Json.JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["Area"] = "PRODUCCION" });
        var secondAction = new GroqAction
        {
            Action = "start_task",
            // El modelo NO repite projectId/statusId/name/assigneeName/startDate
            Params = new Dictionary<string, object> { ["customFields"] = secondTurnFields }
        };

        var secondResult = await handler.ExecuteAsync(secondAction, null, conversationContext);

        Assert.Contains("ID: 606", secondResult);
        _startTaskCommandMock.Verify(c => c.Execute(It.Is<StarTaskRequest>(r =>
            r.ProjectId == 1 &&
            r.StatusId == 1 &&
            r.Name == "New Task" &&
            r.AssigneeId == 30 &&
            r.StartDate == new DateOnly(2026, 6, 13) &&
            r.CustomFieldOptionIds != null &&
            r.CustomFieldOptionIds["customField3"] == 7)), Times.Once);
        Assert.Null(conversationContext.PendingStartTaskDraft);
    }
}
