using System.Text;
using System.Text.Json;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.Project;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.OpenProjectEntities.User;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class GroqIntegrationTests
{
    private readonly Mock<ILogger<GroqIntentService>> _loggerMock;
    private readonly Mock<IConversationContextService> _conversationContextMock;
    private readonly Mock<IStartTaskCommand> _startTaskMock;
    private readonly Mock<IEndTaskSessionCommand> _endTaskSessionMock;
    private readonly Mock<IStatusOpService> _statusOpMock;
    private readonly Mock<IUserOpService> _userOpService;
    private readonly Mock<IActivityOpService> _activityOpMock;
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageMock;
    private readonly Mock<IProjectOpService> _projectOpMock;
    private readonly Mock<ICustomFieldService> _customFieldMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;

    public GroqIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<GroqIntentService>>();
        _conversationContextMock = new Mock<IConversationContextService>();
        _startTaskMock = new Mock<IStartTaskCommand>();
        _endTaskSessionMock = new Mock<IEndTaskSessionCommand>();
        _statusOpMock = new Mock<IStatusOpService>();
        _userOpService = new Mock<IUserOpService>();
        _activityOpMock = new Mock<IActivityOpService>();
        _updateWorkPackageMock = new Mock<IUpdateWorkPackageCommand>();
        _projectOpMock = new Mock<IProjectOpService>();
        _customFieldMock = new Mock<ICustomFieldService>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();

        _conversationContextMock.Setup(x => x.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Dto.Conversation.ConversationContext { SessionId = "test-session" });
    }

    [Fact]
    public async Task GetIntentAsync_CreateTaskComplexPrompt_ResolvesAllNamesAndExecutesStartTask()
    {
        // ARRANGE
        var settings = Options.Create(new GroqSettings
        {
            ApiKey = "REAL_OR_MOCK_KEY", 
            Model = "llama-3.3-70b-versatile",
            HttpClientName = "GroqClient",
            BaseUrl = "https://api.groq.com/openai/v1/chat/completions"
        });

        var mockHandler = new Mock<HttpMessageHandler>();
        var groqJsonResponse = @"{
            ""choices"": [
                {
                    ""message"": {
                        ""content"": ""{ \""action\"": \""start_task\"", \""params\"": { \""projectName\"": \""eProduction\"", \""name\"": \""Integracion con OP\"", \""assigneeName\"": \""Stin Sanchez\"", \""responsibleName\"": \""Stin Sanchez\"", \""startDate\"": \""2026-05-01\"", \""dueDate\"": \""2026-05-04\"", \""areaName\"": \""Soporte\"", \""moduleName\"": \""Nominas\"" } }""
                    }
                }
            ]
        }";

        mockHandler.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).ReturnsAsync(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(groqJsonResponse, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("GroqClient")).Returns(httpClient);

        // Mocks de servicios de resolución
        _projectOpMock.Setup(x => x.FindByName("eProduction")).ReturnsAsync(new Project { Id = 10, Name = "eProduction" });
        _userOpService.Setup(x => x.FindByName("Stin Sanchez")).ReturnsAsync(new User { Id = 5, Name = "Stin Sanchez" });
        _statusOpMock.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(new Status { Id = 1, Name = "New" });
        _customFieldMock.Setup(x => x.FindAreaByName("Soporte")).ReturnsAsync(new CustomOption { Id = 3, Value = "Soporte" });
        _customFieldMock.Setup(x => x.FindModuleByName("Nominas")).ReturnsAsync(new CustomOption { Id = 5, Value = "Nominas" });

        _startTaskMock.Setup(x => x.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 999, Name = "Integracion con OP" });

        var service = new GroqIntentService(
            _loggerMock.Object,
            settings,
            _httpClientFactoryMock.Object,
            _conversationContextMock.Object,
            _startTaskMock.Object,
            _endTaskSessionMock.Object,
            _statusOpMock.Object,
            _userOpService.Object,
            _activityOpMock.Object,
            _updateWorkPackageMock.Object,
            _projectOpMock.Object,
            _customFieldMock.Object,
            new List<IHeuristicIntentHandler>()
        );

        // ACT
        string prompt = "Crea una nueva tarea en el proyecto eProduction , ponle de nombre 'Integracion con OP' , asignala a Stin Sanchez y pon como responsable a Stin Sanchez , ponle de fecha de inicio hoy y fecha de fin el Lunes , usa el área Soporte y el módulo Nominas y empieza a trackear el tiempo.";
        var result = await service.GetIntentAsync(prompt, "test-session");

        // ASSERT
        Assert.Contains("ID: 999", result);
        _projectOpMock.Verify(x => x.FindByName("eProduction"), Times.AtLeastOnce);
        _userOpService.Verify(x => x.FindByName("Stin Sanchez"), Times.AtLeastOnce);
        _startTaskMock.Verify(x => x.Execute(It.Is<StarTaskRequest>(r => 
            r.ProjectId == 10 && 
            r.Name == "Integracion con OP" && 
            r.AssigneeId == 5 && 
            r.ResponsibleId == 5 &&
            r.Area == "3" &&
            r.Module == "5"
        )), Times.Once);
    }
}
