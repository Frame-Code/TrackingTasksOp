using System.Net;
using System.Text;
using System.Text.Json;
using Application.Dto.Conversation;
using Application.Dto.ListWorkPackages;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class GroqIntentServiceTests
{
    private readonly Mock<ILogger<GroqIntentService>> _loggerMock;
    private readonly Mock<IConversationContextService> _conversationContextMock;
    private readonly Mock<IStartTaskCommand> _startTaskMock;
    private readonly Mock<IEndTaskSessionCommand> _endTaskSessionMock;
    private readonly Mock<IStatusOpService> _statusOpMock;
    private readonly Mock<IUserOpService> _userOpMock;
    private readonly Mock<IActivityOpService> _activityOpMock;
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageMock;
    private readonly Mock<IProjectOpService> _projectOpMock;
    private readonly Mock<ICustomFieldService> _customFieldMock;
    private readonly List<Mock<IHeuristicIntentHandler>> _heuristicHandlerMocks;
    private readonly IOptions<GroqSettings> _settings;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public GroqIntentServiceTests()
    {
        _loggerMock = new Mock<ILogger<GroqIntentService>>();
        _conversationContextMock = new Mock<IConversationContextService>();
        _startTaskMock = new Mock<IStartTaskCommand>();
        _endTaskSessionMock = new Mock<IEndTaskSessionCommand>();
        _statusOpMock = new Mock<IStatusOpService>();
        _userOpMock = new Mock<IUserOpService>();
        _activityOpMock = new Mock<IActivityOpService>();
        _updateWorkPackageMock = new Mock<IUpdateWorkPackageCommand>();
        _projectOpMock = new Mock<IProjectOpService>();
        _customFieldMock = new Mock<ICustomFieldService>();
        _heuristicHandlerMocks = new List<Mock<IHeuristicIntentHandler>>();

        _settings = Options.Create(new GroqSettings
        {
            ApiKey = "test-api-key",
            Model = "llama3-8b-8192",
            HttpClientName = "GroqClient",
            BaseUrl = "https://api.groq.com/openai/v1/chat/completions"
        });

        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _conversationContextMock.Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConversationContext { SessionId = "test-session" });
    }

    private GroqIntentService CreateService()
    {
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(x => x.CreateClient("GroqClient")).Returns(_httpClient);

        return new GroqIntentService(
            _loggerMock.Object,
            _settings,
            httpClientFactoryMock.Object,
            _conversationContextMock.Object,
            _startTaskMock.Object,
            _endTaskSessionMock.Object,
            _statusOpMock.Object,
            _userOpMock.Object,
            _activityOpMock.Object,
            _updateWorkPackageMock.Object,
            _projectOpMock.Object,
            _customFieldMock.Object,
            _heuristicHandlerMocks.Select(m => m.Object)
        );
    }

    private void SetupGroqResponse(string content)
    {
        var responseObj = new
        {
            choices = new[]
            {
                new { message = new { content = content } }
            }
        };

        var responseMessage = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(responseMessage);
    }

    [Fact]
    public async Task GetIntentAsync_HeuristicHandlerMatches_ReturnsHeuristicResult()
    {
        var handlerMock = new Mock<IHeuristicIntentHandler>();
        handlerMock.Setup(x => x.HandleAsync("test prompt")).ReturnsAsync("Heuristic Result");
        _heuristicHandlerMocks.Add(handlerMock);
        
        var service = CreateService();

        var result = await service.GetIntentAsync("test prompt", "test-session");

        Assert.Equal("Heuristic Result", result);
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetIntentAsync_NoHeuristicMatches_CallsLLM()
    {
        var handlerMock = new Mock<IHeuristicIntentHandler>();
        handlerMock.Setup(x => x.HandleAsync(It.IsAny<string>())).ReturnsAsync((string)null!);
        _heuristicHandlerMocks.Add(handlerMock);
        
        SetupGroqResponse("LLM Response");
        var service = CreateService();

        var result = await service.GetIntentAsync("test prompt", "test-session");

        Assert.Equal("LLM Response", result);
        _httpMessageHandlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetIntentAsync_StartTask_ValidResponse_ExecutesCommand()
    {
        var service = CreateService();
        
        SetupGroqResponse(@"{ ""action"": ""start_task"", ""params"": { ""projectId"": 1, ""statusName"": ""New"", ""name"": ""New Task"" } }");

        _statusOpMock.Setup(x => x.FindByNameAsync("New")).ReturnsAsync(new Domain.Entities.OpenProjectEntities.Status.Status { Id = 2, Name = "New" });

        _startTaskMock.Setup(x => x.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 123, Name = "New Task" });

        var result = await service.GetIntentAsync("crea la tarea New Task en el proyecto 1 con estado New", "test-session");

        Assert.Contains("ID: 123", result);
        Assert.Contains("New Task", result);
        _startTaskMock.Verify(x => x.Execute(It.Is<StarTaskRequest>(r => r.Name == "New Task" && r.ProjectId == 1 && r.StatusId == 2)), Times.Once);
    }

    [Fact]
    public async Task GetIntentAsync_EndTaskSession_ValidResponse_ExecutesCommand()
    {
        var service = CreateService();
        
        SetupGroqResponse(@"{ ""action"": ""end_task_session"", ""params"": { ""workPackageId"": 123, ""activityName"": ""Dev"", ""comment"": ""Done"", ""newStatusName"": ""Closed"" } }");

        _statusOpMock.Setup(x => x.FindByNameAsync("Closed")).ReturnsAsync(new Domain.Entities.OpenProjectEntities.Status.Status { Id = 3, Name = "Closed" });
        
        var activity = new Domain.Entities.OpenProjectEntities.Activity.ActivityAllowedValue();
        _activityOpMock.Setup(x => x.FindByNameAsync("Dev", 123)).ReturnsAsync(activity);

        var result = await service.GetIntentAsync("finaliza tarea 123", "test-session");

        Assert.Contains("finalizada", result);
        _endTaskSessionMock.Verify(x => x.Execute(It.Is<EndTaskSessionRequest>(r => r.WorkPackageId == 123 && r.Comment == "Done" && r.NewStatusId == 3)), Times.Once);
    }

    [Fact]
    public async Task GetIntentAsync_AssignUser_ValidResponse_ExecutesCommand()
    {
        var service = CreateService();
        
        SetupGroqResponse(@"{ ""action"": ""assign_user_to_task"", ""params"": { ""workPackageId"": 123, ""assigneeName"": ""John"" } }");

        _userOpMock.Setup(x => x.FindByName("John")).ReturnsAsync(new Domain.Entities.OpenProjectEntities.User.User { Id = 5, Name = "John" });

        var result = await service.GetIntentAsync("asigna a john a la tarea 123", "test-session");

        Assert.Contains("actualizada", result);
        _updateWorkPackageMock.Verify(x => x.Execute(123, null, 5, null), Times.Once);
    }
}
