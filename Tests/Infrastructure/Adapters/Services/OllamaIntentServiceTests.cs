using Application.Dto.Conversation;
using Application.Dto.ListWorkPackages;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using OpenProjectProject = Domain.Entities.OpenProjectEntities.Project.Project;
using Domain.Entities.TrackingTasksEntities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class OllamaIntentServiceTests
{
    private readonly Mock<ILogger<OllamaIntentService>> _loggerMock;
    private readonly Mock<IConversationContextService> _conversationContextMock;
    private readonly Mock<IStartTaskCommand> _startTaskMock;
    private readonly Mock<IEndTaskSessionCommand> _endTaskSessionMock;
    private readonly Mock<IListsWorkPackagesCommand> _listsWorkPackagesMock;
    private readonly Mock<IProjectOpService> _projectOpMock;
    private readonly Mock<IStatusOpService> _statusOpMock;
    private readonly Mock<IUserOpService> _userOpService;
    private readonly Mock<IActivityOpService> _activityOpMock;
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly IOptions<OllamaSettings> _settings;

    public OllamaIntentServiceTests()
    {
        _loggerMock = new Mock<ILogger<OllamaIntentService>>();
        _conversationContextMock = new Mock<IConversationContextService>();
        _startTaskMock = new Mock<IStartTaskCommand>();
        _endTaskSessionMock = new Mock<IEndTaskSessionCommand>();
        _listsWorkPackagesMock = new Mock<IListsWorkPackagesCommand>();
        _projectOpMock = new Mock<IProjectOpService>();
        _statusOpMock = new Mock<IStatusOpService>();
        _userOpService = new Mock<IUserOpService>();
        _activityOpMock = new Mock<IActivityOpService>();
        _updateWorkPackageMock = new Mock<IUpdateWorkPackageCommand>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        _settings = Options.Create(new OllamaSettings
        {
            BaseUrl = "http://localhost:11434",
            Model = "phi3"
        });

        _conversationContextMock.Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationContext { SessionId = "test-session" });
            
        // Setup default HttpClient
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    private OllamaIntentService CreateService()
    {
        return new OllamaIntentService(
            _loggerMock.Object,
            _settings,
            _httpClientFactoryMock.Object,
            _conversationContextMock.Object,
            _startTaskMock.Object,
            _endTaskSessionMock.Object,
            _listsWorkPackagesMock.Object,
            projectOpService: _projectOpMock.Object,
            statusOpService: _statusOpMock.Object,
            userOpService: _userOpService.Object,
            activityOpService: _activityOpMock.Object,
            updateWorkPackageCommand: _updateWorkPackageMock.Object
        );
    }

    [Fact]
    public async Task GetIntentAsync_Heuristic_Proyectos_ReturnsList()
    {
        // ARRANGE
        var service = CreateService();
        var projects = new List<OpenProjectProject>
        {
            new OpenProjectProject { Id = 1, Name = "Project Alpha" },
            new OpenProjectProject { Id = 2, Name = "Project Beta" }
        };
        _projectOpMock.Setup(x => x.Lists()).ReturnsAsync(projects);

        // ACT
        var result = await service.GetIntentAsync("proyectos", "session-1");

        // ASSERT
        Assert.Contains("Project Alpha", result);
        Assert.Contains("Project Beta", result);
        _projectOpMock.Verify(x => x.Lists(), Times.Once);
    }

    [Fact]
    public async Task GetIntentAsync_Heuristic_Tareas_ReturnsList()
    {
        // ARRANGE
        var service = CreateService();
        var workPackages = new List<Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackage>
        {
            new Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackage 
            { 
                Id = 101, 
                Subject = "Task 1", 
                Links = new Domain.Entities.OpenProjectEntities.WorkPackage.WorkPackageLinks 
                { 
                    Status = new Domain.Entities.OpenProjectEntities.LinkObject { Title = "In Progress" },
                    Project = new Domain.Entities.OpenProjectEntities.LinkObject { Title = "Project A" }
                } 
            }
        };
        _listsWorkPackagesMock.Setup(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>())).ReturnsAsync(workPackages);

        // ACT
        var result = await service.GetIntentAsync("tareas", "session-1");

        // ASSERT
        Assert.Contains("Task 1", result);
        Assert.Contains("In Progress", result);
        _listsWorkPackagesMock.Verify(x => x.Execute(It.IsAny<ListsWorkPackagesRequest>()), Times.Once);
    }

    [Fact]
    public async Task GetIntentAsync_ComplexIntent_StartTask_ExecutesCommand()
    {
        // ARRANGE
        var mockHandler = new Mock<HttpMessageHandler>();
        
        // NDJSON response for Ollama
        var responseContent = "{\"model\":\"phi3\",\"message\":{\"role\":\"assistant\",\"content\":\"{ \\\"action\\\": \\\"start_task\\\", \\\"params\\\": { \\\"projectId\\\": 1, \\\"statusId\\\": 1, \\\"name\\\": \\\"Test Task\\\" } }\"},\"done\":true}\n";
        
        mockHandler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("OllamaClient")).Returns(httpClient);

        _startTaskMock.Setup(x => x.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 123, Name = "Test Task" });

        var service = CreateService();

        // ACT
        var result = await service.GetIntentAsync("Crea una tarea Test Task", "session-1");

        // ASSERT
        Assert.Contains("Tarea #123 creada", result);
        _startTaskMock.Verify(x => x.Execute(It.Is<StarTaskRequest>(r => r.Name == "Test Task" && r.ProjectId == 1)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAction_StartTask_CallsStartTaskCommand()
    {
        var service = CreateService();
        
        // Obtenemos el tipo privado OllamaAction por reflexión
        var ollamaActionType = typeof(OllamaIntentService).GetNestedType("OllamaAction", BindingFlags.NonPublic);
        var actionInstance = Activator.CreateInstance(ollamaActionType!);
        
        ollamaActionType!.GetProperty("Action")!.SetValue(actionInstance, "start_task");
        var paramsDict = new Dictionary<string, object>
        {
            ["projectId"] = 5,
            ["statusId"] = 1,
            ["name"] = "Reflected Task"
        };
        ollamaActionType.GetProperty("Params")!.SetValue(actionInstance, paramsDict);

        _startTaskMock.Setup(x => x.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 99, Name = "Reflected Task" });

        // Invocamos el método privado ExecuteAction
        var method = typeof(OllamaIntentService).GetMethod("ExecuteAction", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { actionInstance! })!;
        var result = await task;

        Assert.IsType<Domain.Entities.TrackingTasksEntities.Task>(result);
        _startTaskMock.Verify(x => x.Execute(It.Is<StarTaskRequest>(r => r.ProjectId == 5 && r.Name == "Reflected Task")), Times.Once);
    }
}
