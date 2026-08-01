using Application.Dto.Conversation;
using Application.Ports.Services;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.Services.Bot;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class GroqIntentServiceTests
{
    private readonly Mock<ILogger<GroqIntentService>> _loggerMock = new();
    private readonly Mock<IConversationContextService> _contextServiceMock = new();
    private readonly Mock<IBotIntentInterceptor> _interceptorMock = new();
    private readonly Mock<IGroqApiClient> _groqApiClientMock = new();
    private readonly Mock<IBotActionExecutor> _botActionExecutorMock = new();

    private GroqIntentService BuildService() => new(
        _loggerMock.Object,
        _contextServiceMock.Object,
        _interceptorMock.Object,
        _groqApiClientMock.Object,
        _botActionExecutorMock.Object);

    private void SetupContext(string sessionId, ConversationContext context)
    {
        _contextServiceMock.Setup(s => s.GetOrCreateAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(context);
        _interceptorMock.Setup(i => i.Normalize(It.IsAny<string>())).Returns<string>(p => p.ToLowerInvariant().Trim());
    }

    [Fact]
    public async Task GetIntentAsync_HeuristicMatches_ShouldShortCircuitAndNotCallGroq()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync("listar proyectos", It.IsAny<CancellationToken>()))
            .ReturnsAsync("📋 **Tus Proyectos Disponibles:**\n\n- **Proyecto A** (ID: 1)");

        var service = BuildService();
        var result = await service.GetIntentAsync("Listar proyectos", "session1");

        Assert.Contains("Proyecto A", result);
        _groqApiClientMock.Verify(c => c.GetCompletionAsync(It.IsAny<ConversationContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _contextServiceMock.Verify(s => s.SaveAsync(context, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetIntentAsync_ApiKeyNotConfigured_ShouldReturnWarning()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(false);

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        Assert.Contains("API Key de Groq no configurada", result);
        _groqApiClientMock.Verify(c => c.GetCompletionAsync(It.IsAny<ConversationContext>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetIntentAsync_GroqResponseWithoutAction_ShouldReturnAsIs()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "hola", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqCompletionResult { Text = "Hola, ¿en qué puedo ayudarte?" });

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        Assert.Equal("Hola, ¿en qué puedo ayudarte?", result);
        _botActionExecutorMock.Verify(e => e.ExecuteAllAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<ConversationContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetIntentAsync_GroqResponseWithAction_ShouldDelegateToExecutorAndSaveContext()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);

        var aiResponse = "{\"action\": \"list_projects\"}";
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "listar proyectos", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqCompletionResult { Text = aiResponse });
        _botActionExecutorMock.Setup(e => e.ExecuteAllAsync(It.Is<IEnumerable<string>>(blocks => blocks.Single() == aiResponse), context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["📋 **Tus Proyectos Disponibles:**\n\n- **Proyecto A** (ID: 1)"]);

        var service = BuildService();
        var result = await service.GetIntentAsync("listar proyectos", "session1");

        Assert.Contains("Proyecto A", result);
        _contextServiceMock.Verify(s => s.SaveAsync(context, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, context.History.Count);
        Assert.Equal("ttm", context.History[1].Type);
    }

    [Fact]
    public async Task GetIntentAsync_GroqReturnsToolCall_ShouldBuildJsonBlockAndDelegateToExecutor()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);

        var toolCall = new GroqToolCall
        {
            Id = "call_1",
            Name = "start_task",
            ArgumentsJson = "{\"projectName\":\"eProduction\",\"name\":\"Test\"}"
        };
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "crea una tarea", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqCompletionResult { Text = "", ToolCalls = [toolCall] });

        _botActionExecutorMock.Setup(e => e.ExecuteAllAsync(
                It.Is<IEnumerable<string>>(blocks =>
                    blocks.Single().Contains("\"action\":\"start_task\"") &&
                    blocks.Single().Contains("\"projectName\":\"eProduction\"")),
                context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["🚀 Tarea **Test** preparada y seguimiento iniciado (ID: 202)."]);

        var service = BuildService();
        var result = await service.GetIntentAsync("crea una tarea", "session1");

        Assert.Contains("ID: 202", result);
    }

    [Fact]
    public async Task GetIntentAsync_GroqApiClientThrows_ShouldReturnConnectionError()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "hola", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("timeout"));

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        Assert.Contains("❌ Error al conectar", result);
        Assert.Contains("timeout", result);
    }
}
