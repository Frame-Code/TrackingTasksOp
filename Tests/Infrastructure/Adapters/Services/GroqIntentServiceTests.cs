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
            .ThrowsAsync(new Exception("timeout at 10.0.0.5:443"));

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        Assert.Contains("No pude comunicarme con el asistente", result);
        // El detalle técnico va al log, no al chat: acá viajaban IPs, puertos y stack traces.
        Assert.DoesNotContain("timeout", result);
        Assert.DoesNotContain("10.0.0.5", result);
    }

    [Fact]
    public async Task GetIntentAsync_LongActionResult_ShouldKeepItForTheUserButSummarizeItForTheModel()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);

        // Una lista como la que devuelve list_tasks en la instancia real: 30 tareas.
        var longList = string.Join("\n", Enumerable.Range(1000, 30)
            .Select(id => $"#{id}: Tarea de ejemplo número {id} con un asunto largo — Developed"));

        var aiResponse = "{\"action\": \"list_tasks\"}";
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "lista mis tareas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqCompletionResult { Text = aiResponse });
        _botActionExecutorMock.Setup(e => e.ExecuteAllAsync(It.IsAny<IEnumerable<string>>(), context, It.IsAny<CancellationToken>()))
            .ReturnsAsync([longList]);

        var service = BuildService();
        var result = await service.GetIntentAsync("lista mis tareas", "session1");

        var stored = context.History[1];

        // El usuario ve todo.
        Assert.Contains("#1029", result);
        Assert.Contains("#1029", stored.Content);

        // El modelo recibe solo el resumen: es lo que evitaba arrastrar la lista 4 turnos.
        Assert.NotNull(stored.ModelContent);
        Assert.Contains("list_tasks", stored.ContentForModel());
        Assert.DoesNotContain("#1029", stored.ContentForModel());
        Assert.True(stored.ContentForModel().Length < stored.Content.Length / 4);
    }

    [Fact]
    public async Task GetIntentAsync_ShortControlMessage_ShouldReachTheModelVerbatim()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);

        // Las reglas 11 y 12 del system prompt le piden al modelo reconocer estos mensajes
        // LITERALMENTE para continuar el flujo. Si se resumieran, se romperían el conflicto de
        // sesión activa y la creación de tareas con campos faltantes.
        const string conflict = "⏸️ Ya tienes la tarea #1134 corriendo. ¿Subo el tiempo a OpenProject o lo guardo en local?";

        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "empezá la #900", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroqCompletionResult { Text = "{\"action\": \"start_task\"}" });
        _botActionExecutorMock.Setup(e => e.ExecuteAllAsync(It.IsAny<IEnumerable<string>>(), context, It.IsAny<CancellationToken>()))
            .ReturnsAsync([conflict]);

        var service = BuildService();
        await service.GetIntentAsync("empezá la #900", "session1");

        var stored = context.History[1];

        Assert.Null(stored.ModelContent);
        Assert.Contains("Ya tienes la tarea #1134 corriendo", stored.ContentForModel());
    }

    [Fact]
    public async Task GetIntentAsync_RateLimited_ShouldTellUserToWaitWithoutLeakingTheBody()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "hola", It.IsAny<CancellationToken>()))
            .ThrowsAsync(GroqApiException.FromResponse(
                System.Net.HttpStatusCode.TooManyRequests,
                """{"error":{"message":"Rate limit reached for model `openai/gpt-oss-120b` in organization `org_01kngd`","code":"rate_limit_exceeded"}}"""));

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        Assert.Contains("Esperá unos segundos", result);
        // Nada de JSON, nombre de modelo ni ID de organización en pantalla.
        Assert.DoesNotContain("rate_limit_exceeded", result);
        Assert.DoesNotContain("org_01kngd", result);
        Assert.DoesNotContain("{", result);
    }

    [Fact]
    public async Task GetIntentAsync_BadApiKey_ShouldPointUserToSettings()
    {
        var context = new ConversationContext { SessionId = "session1" };
        SetupContext("session1", context);
        _interceptorMock.Setup(i => i.TryInterceptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        _groqApiClientMock.Setup(c => c.IsConfigured).Returns(true);
        _groqApiClientMock.Setup(c => c.GetCompletionAsync(context, "hola", It.IsAny<CancellationToken>()))
            .ThrowsAsync(GroqApiException.FromResponse(
                System.Net.HttpStatusCode.Unauthorized,
                """{"error":{"message":"Invalid API Key"}}"""));

        var service = BuildService();
        var result = await service.GetIntentAsync("hola", "session1");

        // Un fallo de credenciales el usuario SÍ lo puede resolver: hay que decirle dónde.
        Assert.Contains("Configuración", result);
        Assert.DoesNotContain("Invalid API Key", result);
    }
}
