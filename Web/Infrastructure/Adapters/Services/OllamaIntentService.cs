using Application.Dto.ListWorkPackages;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Text.Json;
using System.Threading;
using System.Linq;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

/// <summary>
/// Implementación que utiliza un LLM local (Phi-3 Mini) via Ollama para procesar intenciones.
/// Respeta el patrón de Adaptador y el Principio Abierto/Cerrado.
/// </summary>
public class OllamaIntentService : IGeminiIntentService
{
    private readonly ILogger<OllamaIntentService> _logger;
    private readonly OllamaSettings _ollamaSettings;
    private readonly IConversationContextService _conversationContextService;
    private readonly OllamaApiClient _ollamaClient;

    // Servicios de Negocio (Commands/Services)
    private readonly IStartTaskCommand _startTaskCommand;
    private readonly IEndTaskSessionCommand _endTaskSessionCommand;
    private readonly IListsWorkPackagesCommand _listsWorkPackagesCommand;
    private readonly IProjectOpService _projectOpService;
    private readonly IStatusOpService _statusOpService;
    private readonly IUserOpService _userOpService;
    private readonly IActivityOpService _activityOpService;
    private readonly IUpdateWorkPackageCommand _updateWorkPackageCommand;

    public OllamaIntentService(
        ILogger<OllamaIntentService> logger,
        IOptions<OllamaSettings> ollamaSettings,
        IConversationContextService conversationContextService,
        IStartTaskCommand startTaskCommand,
        IEndTaskSessionCommand endTaskSessionCommand,
        IListsWorkPackagesCommand listsWorkPackagesCommand,
        IProjectOpService projectOpService,
        IStatusOpService statusOpService,
        IUserOpService userOpService,
        IActivityOpService activityOpService,
        IUpdateWorkPackageCommand updateWorkPackageCommand)
    {
        _logger = logger;
        _ollamaSettings = ollamaSettings.Value;
        _conversationContextService = conversationContextService;
        _startTaskCommand = startTaskCommand;
        _endTaskSessionCommand = endTaskSessionCommand;
        _listsWorkPackagesCommand = listsWorkPackagesCommand;
        _projectOpService = projectOpService;
        _statusOpService = statusOpService;
        _userOpService = userOpService;
        _activityOpService = activityOpService;
        _updateWorkPackageCommand = updateWorkPackageCommand;

        // Inicializar cliente local de Ollama con Timeout infinito
        // Los LLMs locales pueden tardar más de los 100s por defecto de HttpClient
        var httpClient = new System.Net.Http.HttpClient { 
            BaseAddress = new Uri(_ollamaSettings.BaseUrl),
            Timeout = Timeout.InfiniteTimeSpan 
        };
        _ollamaClient = new OllamaApiClient(httpClient);
        _ollamaClient.SelectedModel = _ollamaSettings.Model;
    }

    public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
    {
        var context = await _conversationContextService.GetOrCreateAsync(sessionId, ct);
        string lowerPrompt = prompt.ToLowerInvariant().Trim();

        // ==============================================================================
        // 1. CAPA DE INTERCEPTACIÓN HEURÍSTICA (AHORRO DE RECURSOS)
        // ==============================================================================
        
        // Listar Proyectos
        if (lowerPrompt == "proyectos" || lowerPrompt.Contains("listar proyectos") || lowerPrompt.Contains("mis proyectos"))
        {
            var projects = await _projectOpService.Lists();
            string resp = "📋 **Tus Proyectos Disponibles:**\n\n" + string.Join("\n", projects.Select(p => $"- **{p.Name}** (ID: {p.Id})"));
            return await SaveContext(context, prompt, resp, ct);
        }

        // Listar Tareas (UX Agrupada)
        if (lowerPrompt == "tareas" || lowerPrompt.Contains("mis tareas") || lowerPrompt.Contains("qué tengo pendiente") || lowerPrompt.Contains("que tengo pendiente"))
        {
            var wps = await _listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50));
            if (!wps.Any()) return await SaveContext(context, prompt, "✅ No tienes tareas pendientes.", ct);

            var builder = new System.Text.StringBuilder("📝 **Tus Tareas Pendientes:**\n\n");
            var groups = wps.GroupBy(w => w.Links?.Project?.Title ?? "Otros");
            foreach (var g in groups)
            {
                builder.AppendLine($"📁 **{g.Key}**");
                foreach (var t in g) builder.AppendLine($"- **#{t.Id}**: {t.Subject} *({t.Links?.Status?.Title})*");
                builder.AppendLine();
            }
            return await SaveContext(context, prompt, builder.ToString().TrimEnd(), ct);
        }

        // Listar Usuarios
        if (lowerPrompt.Contains("mostrar usuarios") || lowerPrompt.Contains("listar usuarios") || lowerPrompt == "usuarios")
        {
            var users = await _userOpService.Lists();
            string resp = "👥 **Usuarios:**\n\n" + string.Join("\n", users.Select(u => $"- {u.Name} (ID: {u.Id})"));
            return await SaveContext(context, prompt, resp, ct);
        }

        // ==============================================================================
        // 2. CAPA DE LLM LOCAL (OLLAMA + PHI-3)
        // ==============================================================================
        _logger.LogInformation("Consultando a Ollama ({Model}) para intención compleja.", _ollamaSettings.Model);

        var systemPrompt = @"Eres un asistente experto en TrackingTasksOp para OpenProject.
REGLAS ESTRICTAS:
1. Para realizar acciones (crear, asignar, finalizar), responde ÚNICAMENTE con uno o varios objetos JSON (uno por línea).
2. NUNCA uses 'null' para projectId o statusId. Si no los conoces, pídelos al usuario o búscalos en el historial.
3. Formato de JSON: { ""action"": ""NAME"", ""params"": { ""KEY"": ""VALUE"" } }

Acciones:
- start_task: { ""projectId"": int, ""statusId"": int, ""name"": string } -> Crea e inicia trackeo.
- assign_user_to_task: { ""workPackageId"": int, ""assigneeName"": string, ""responsibleName"": string }
- end_task_session: { ""workPackageId"": int, ""activityId"": int, ""comment"": string }

Si el usuario pide varias cosas (ej. 'crea y asigna'), envía un JSON por cada acción.
Si es una charla normal, responde en texto plano.";

        var messages = new List<Message> { new Message(ChatRole.System, systemPrompt) };
        foreach (var h in context.History.TakeLast(6)) // Últimos 6 para no saturar contexto
        {
            messages.Add(new Message(h.Type == "user" ? ChatRole.User : ChatRole.Assistant, h.Content));
        }
        messages.Add(new Message(ChatRole.User, prompt));

        string aiResponse = "";
        await foreach (var answer in _ollamaClient.ChatAsync(new ChatRequest 
        { 
            Model = _ollamaSettings.Model, 
            Messages = messages,
            Options = new OllamaSharp.Models.RequestOptions { Temperature = 0.1f }
        }, ct))
        {
            if (answer?.Message != null) aiResponse += answer.Message.Content;
        }

        // --- PROCESADOR MULTI-ACCIÓN ---
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var executionSummary = new List<string>();
        int lastCreatedWpId = 0;

        foreach (var line in lines)
        {
            string cleanLine = line.Trim().TrimEnd(',');
            if (cleanLine.StartsWith("{") && cleanLine.Contains("\"action\""))
            {
                try 
                {
                    var actionData = JsonSerializer.Deserialize<OllamaAction>(cleanLine, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (actionData != null)
                    {
                        // Si la acción anterior creó un WP, intentamos usarlo si la actual viene con ID 0 o null
                        if (lastCreatedWpId > 0 && GetInt(actionData.Params, "workPackageId") <= 0)
                        {
                            actionData.Params ??= new Dictionary<string, object>();
                            actionData.Params["workPackageId"] = lastCreatedWpId;
                        }

                        var result = await ExecuteAction(actionData);
                        
                        // Extraer ID si se creó una tarea
                        if (actionData.Action == "start_task" && result is Domain.Entities.TrackingTasksEntities.Task t)
                            lastCreatedWpId = t.WorkPackageId;

                        executionSummary.Add($"✅ Ejecutado: {actionData.Action}");
                    }
                }
                catch (Exception ex) { _logger.LogWarning("Error procesando línea JSON: {Msg}", ex.Message); }
            }
        }

        if (executionSummary.Any())
        {
            string finalMsg = string.Join("\n", executionSummary) + "\n\n¿Deseas algo más?";
            if (lastCreatedWpId > 0) finalMsg = $"🚀 Tarea #{lastCreatedWpId} creada y trackeo iniciado.\n" + finalMsg;
            return await SaveContext(context, prompt, finalMsg, ct);
        }

        return await SaveContext(context, prompt, aiResponse, ct);
    }

    private async Task<object> ExecuteAction(OllamaAction action)
    {
        var p = action.Params;
        switch (action.Action)
        {
            case "start_task":
                return await _startTaskCommand.Execute(new StarTaskRequest {
                    ProjectId = GetInt(p, "projectId"),
                    StatusId = GetInt(p, "statusId"),
                    Name = GetStr(p, "name"),
                    WorkPackageId = GetInt(p, "workPackageId")
                });
            case "assign_user_to_task":
                int wpId = GetInt(p, "workPackageId");
                var user = await _userOpService.FindByName(GetStr(p, "assigneeName"));
                await _updateWorkPackageCommand.Execute(wpId, assigneeId: user?.Id);
                return new { status = "Asignado" };
            case "end_task_session":
                return await _endTaskSessionCommand.Execute(new EndTaskSessionRequest(
                    GetInt(p, "workPackageId"),
                    GetInt(p, "activityId"),
                    GetStr(p, "comment"),
                    null
                ));
            default: return new { error = "Acción no reconocida" };
        }
    }

    private int GetInt(Dictionary<string, object>? dict, string key) 
    {
        if (dict != null && dict.TryGetValue(key, out var val))
        {
            if (val is JsonElement el) 
            {
                if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out int res)) return res;
                return 0;
            }
            return Convert.ToInt32(val);
        }
        return 0;
    }

    private string GetStr(Dictionary<string, object>? dict, string key) => 
        (dict != null && dict.TryGetValue(key, out var val)) ? val?.ToString() ?? "" : "";

    private async Task<string> SaveContext(Application.Dto.Conversation.ConversationContext context, string prompt, string response, CancellationToken ct)
    {
        context.AddUserMessage(prompt);
        context.AddTtmMessage(response);
        await _conversationContextService.SaveAsync(context, ct);
        return response;
    }

    private class OllamaAction
    {
        public string Action { get; set; } = "";
        public Dictionary<string, object>? Params { get; set; }
    }
}
