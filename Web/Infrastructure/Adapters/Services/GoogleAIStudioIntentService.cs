using Application.Dto.Tasks;
using Application.Dto.ListWorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services;

/// <summary>
/// Implementación que consume Google AI Studio (Gemini API gratuita) usando el SDK oficial de Google.GenAI.
/// </summary>
public class GoogleAIStudioIntentService : IGeminiIntentService
{
    private readonly ILogger<GoogleAIStudioIntentService> _logger;
    private readonly GeminiSettings _geminiSettings;
    private readonly IConversationContextService _conversationContextService;
    private readonly Client _generativeAIClient;

    // Servicios para ejecutar funciones
    private readonly IStartTaskCommand _startTaskCommand;
    private readonly IEndTaskSessionCommand _endTaskSessionCommand;
    private readonly IListsWorkPackagesCommand _listsWorkPackagesCommand;
    private readonly IUpdateWorkPackageCommand _updateWorkPackageCommand;
    private readonly IProjectOpService _projectOpService;
    private readonly IStatusOpService _statusOpService;
    private readonly IActivityOpService _activityOpService;
    private readonly IUserOpService _userOpService;

    public GoogleAIStudioIntentService(
        ILogger<GoogleAIStudioIntentService> logger,
        IOptions<GeminiSettings> geminiSettings,
        IConversationContextService conversationContextService,
        IStartTaskCommand startTaskCommand,
        IEndTaskSessionCommand endTaskSessionCommand,
        IListsWorkPackagesCommand listsWorkPackagesCommand,
        IUpdateWorkPackageCommand updateWorkPackageCommand,
        IProjectOpService projectOpService,
        IStatusOpService statusOpService,
        IActivityOpService activityOpService,
        IUserOpService userOpService)
    {
        _logger = logger;
        _geminiSettings = geminiSettings.Value;
        _conversationContextService = conversationContextService;
        _startTaskCommand = startTaskCommand;
        _endTaskSessionCommand = endTaskSessionCommand;
        _listsWorkPackagesCommand = listsWorkPackagesCommand;
        _updateWorkPackageCommand = updateWorkPackageCommand;
        _projectOpService = projectOpService;
        _statusOpService = statusOpService;
        _activityOpService = activityOpService;
        _userOpService = userOpService;

        // VALIDACIÓN DEFENSIVA (Fail-Fast)
        if (string.IsNullOrWhiteSpace(_geminiSettings.ApiKey))
        {
            _logger.LogCritical("CRÍTICO: La API Key de Gemini no se cargó desde appsettings.json.");
            throw new System.ArgumentNullException(nameof(_geminiSettings.ApiKey), "La API Key de Gemini no puede estar vacía. Configúrala en GeminiSettings:ApiKey");
        }

        // Inicializamos el SDK oficial de Google GenAI usando SOLO la API Key para Nivel Gratuito (Google AI Studio)
        // Omitimos 'project', 'location' y seteamos vertexAI en false implícitamente al no pasarlo.
        _generativeAIClient = new Client(apiKey: _geminiSettings.ApiKey);
    }

    public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
    {
        var conversationContext = await _conversationContextService.GetOrCreateAsync(sessionId, ct);
        
        try
        {
            _logger.LogInformation("Processing intent for session {SessionId}", sessionId);

            // ==============================================================================
            // 1. CAPA DE INTERCEPTACIÓN HEURÍSTICA (AHORRO DE CUOTA / RATE LIMITING)
            // ==============================================================================
            string lowerPrompt = prompt.ToLowerInvariant().Trim();
            
            // Interceptar: Listar Proyectos
            if (lowerPrompt == "proyectos" || lowerPrompt.Contains("listar proyectos") || lowerPrompt.Contains("mis proyectos") || lowerPrompt.Contains("qué proyectos"))
            {
                var projects = await _projectOpService.Lists();
                string response = "📋 **Tus Proyectos Disponibles:**\n\n";
                foreach(var p in projects) response += $"- **{p.Name}** (ID: {p.Id})\n";
                
                return SaveAndReturnContext(conversationContext, prompt, response, ct).Result;
            }

            // Interceptar: Listar Tareas Pendientes (Generales)
            if (lowerPrompt == "tareas" || lowerPrompt.Contains("mis tareas") || lowerPrompt.Contains("tareas pendientes") || lowerPrompt.Contains("qué tengo pendiente"))
            {
                // Pasamos projectId null para traer las tareas globales asignadas al usuario
                var wps = await _listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50));
                
                if (!wps.Any()) return SaveAndReturnContext(conversationContext, prompt, "✅ ¡Felicidades! No tienes tareas pendientes asignadas en este momento.", ct).Result;

                var builder = new System.Text.StringBuilder();
                builder.AppendLine("📝 **Tus Tareas Pendientes:**\n");

                var groupedTasks = wps.GroupBy(w => w.Links?.Project?.Title ?? "Otros Proyectos");

                foreach (var group in groupedTasks)
                {
                    builder.AppendLine($"📁 **{group.Key}**");
                    foreach (var task in group)
                    {
                        builder.AppendLine($"- **#{task.Id}**: {task.Subject} *(Estado: {task.Links?.Status?.Title})*");
                    }
                    builder.AppendLine();
                }
                
                return SaveAndReturnContext(conversationContext, prompt, builder.ToString().TrimEnd(), ct).Result;
            }
            
            // Interceptar: Listar Usuarios
            if (lowerPrompt == "usuarios" || lowerPrompt.Contains("listar usuarios") || lowerPrompt.Contains("mostrar usuarios") || lowerPrompt.Contains("quiénes están"))
            {
                var users = await _userOpService.Lists();
                string response = "👥 **Usuarios del Sistema:**\n\n";
                foreach(var u in users) response += $"- {u.Name} (ID: {u.Id})\n";
                
                return SaveAndReturnContext(conversationContext, prompt, response, ct).Result;
            }

            // Interceptar: Listar Estados
            if (lowerPrompt == "estados" || lowerPrompt.Contains("listar estados") || lowerPrompt.Contains("ver estados"))
            {
                var statuses = await _statusOpService.Lists();
                string response = "🚦 **Estados Disponibles:**\n\n";
                foreach(var s in statuses) response += $"- {s.Name} (ID: {s.Id})\n";
                
                return SaveAndReturnContext(conversationContext, prompt, response, ct).Result;
            }

            // ==============================================================================
            // 2. CAPA DE INTELIGENCIA ARTIFICIAL (GEMINI - Operaciones Complejas)
            // ==============================================================================
            _logger.LogInformation("Enviando petición a Gemini para análisis complejo.");
            
            // Preparamos el historial (solo si vamos a usar la IA)
            var contents = conversationContext.History
                .Where(item => !string.IsNullOrWhiteSpace(item.Content))
                .Select(item => new Content { 
                    Role = item.Type == "ttm" ? "model" : "user", 
                    Parts = new List<Part> { new Part { Text = item.Content } } 
                }).ToList();
            
            bool finalResponseReached = false;
            string finalResult = string.Empty;
            int loopCounter = 0;
            const int MAX_FUNCTION_CALLS = 5;

            while (!finalResponseReached)
            {
                if (++loopCounter > MAX_FUNCTION_CALLS)
                {
                    _logger.LogWarning("Circuit Breaker: Se alcanzó el límite máximo de llamadas a funciones.");
                    finalResult = "He alcanzado el límite de operaciones internas para esta solicitud. Por favor, intenta ser más específico.";
                    break;
                }

                GenerateContentResponse response;
                try 
                {
                    response = await _generativeAIClient.Models.GenerateContentAsync(
                        model: _geminiSettings.Model ?? "gemini-2.5-flash",
                        contents: contents,
                        // El Config y SysInst ya no van aquí en Google.GenAI, se pasan si se usan, pero en Client se asumen
                        cancellationToken: ct);
                }
                catch (ClientError ex)
                {
                    _logger.LogError(ex, "Gemini ClientError during GenerateContent: {Message}", ex.Message);
                    
                    string errorMsg = ex.Message;
                    
                    if (errorMsg.Contains("429") || errorMsg.Contains("Quota exceeded") || errorMsg.Contains("RESOURCE_EXHAUSTED"))
                    {
                        finalResult = "⏳ Lo siento, he alcanzado el límite de peticiones gratuitas. Por favor, espera unos 60 segundos.";
                        break;
                    }
                    if (errorMsg.Contains("API key expired") || errorMsg.Contains("API_KEY_INVALID"))
                    {
                        finalResult = "🔑 Error de Autenticación: La API Key configurada ha expirado o es inválida. Por favor, genera una nueva en Google AI Studio e ingrésala en appsettings.json.";
                        break;
                    }

                    throw;
                }

                if (response.Candidates == null || response.Candidates.Count == 0)
                {
                    finalResult = "No se recibió una respuesta válida del modelo.";
                    finalResponseReached = true;
                    continue;
                }

                var candidate = response.Candidates[0];
                var candidateContent = candidate.Content;
                
                if (candidateContent?.Parts == null)
                {
                    finalResult = "Respuesta vacía.";
                    finalResponseReached = true;
                    continue;
                }

                // Añadir el mensaje del modelo al historial antes de procesar llamadas
                contents.Add(candidateContent);

                var toolCalls = candidateContent.Parts
                    .Where(p => p.FunctionCall != null)
                    .Select(p => p.FunctionCall!)
                    .ToList();
                
                if (toolCalls.Any())
                {
                    _logger.LogInformation("Processing {Count} function calls from Gemini", toolCalls.Count);

                    var responseParts = new List<Part>();
                    foreach (var call in toolCalls)
                    {
                        _logger.LogInformation("Executing tool call: {FunctionName}", call.Name);
                        
                        object result = await ExecuteFunctionAsync(call);
                        Dictionary<string, object> normalizedResult = NormalizeResult(result);
                        
                        // Prevención Payload Gigante
                        var jsonResult = JsonSerializer.Serialize(normalizedResult);
                        if (jsonResult.Length > 8000) 
                        {
                            normalizedResult = new Dictionary<string, object> { 
                                ["warning"] = "Resultado demasiado largo. Mostrando datos parciales.",
                                ["partial_data"] = jsonResult.Substring(0, 8000) + "... [TRUNCATED]" 
                            };
                        }
                        
                        responseParts.Add(new Part
                        {
                            FunctionResponse = new FunctionResponse
                            {
                                Name = call.Name,
                                Response = normalizedResult
                            }
                        });
                    }

                    contents.Add(new Content { Role = "function", Parts = responseParts });
                }
                else
                {
                    finalResult = response.Text ?? "Operación completada.";
                    finalResponseReached = true;
                }
            }

            return await SaveAndReturnContext(conversationContext, prompt, finalResult, ct);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing Gemini intent for session {SessionId}", sessionId);
            return $"Error técnico: Ocurrió un error inesperado al procesar tu solicitud.";
        }
    }

    private async Task<string> SaveAndReturnContext(Application.Dto.Conversation.ConversationContext context, string prompt, string result, CancellationToken ct)
    {
        context.AddUserMessage(prompt);
        context.AddTtmMessage(result);
        await _conversationContextService.SaveAsync(context, ct);
        return result;
    }

    private Dictionary<string, object> NormalizeResult(object result)
    {
        try
        {
            if (result is System.Collections.IEnumerable && result is not string && result is not IDictionary<string, object>)
            {
                return new Dictionary<string, object> { ["elements"] = result };
            }

            if (result is Dictionary<string, object> dict) return dict;

            var json = JsonSerializer.Serialize(result);
            var decoded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            return decoded ?? new Dictionary<string, object> { ["result"] = result?.ToString() ?? "null" };
        }
        catch
        {
            return new Dictionary<string, object> { ["result"] = result?.ToString() ?? "error" };
        }
    }

    private async Task<object> ExecuteFunctionAsync(FunctionCall call)
    {
        try
        {
            var args = call.Args ?? new Dictionary<string, object>();

            switch (call.Name)
            {
                case "list_projects":
                    return await _projectOpService.Lists();

                case "list_work_packages":
                    var projectId = GetIntArg(args, "projectId");
                    var wps = await _listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(projectId, 0, 50));
                    // OPTIMIZACIÓN CRÍTICA: Proyección mínima
                    return wps.Select(w => new { w.Id, w.Subject, Status = w.Links?.Status?.Title }).ToList();

                case "list_statuses":
                    return await _statusOpService.Lists();

                case "list_activities":
                    var wpId = GetIntArg(args, "workPackageId");
                    return await _activityOpService.Lists(wpId);

                case "list_users":
                    return await _userOpService.Lists();

                case "assign_user_to_task":
                    var wpToAssignId = GetIntArg(args, "workPackageId");
                    var assigneeName = GetStringArg(args, "assigneeName");
                    var responsibleName = GetStringArg(args, "responsibleName");
                    
                    int? assigneeId = null;
                    if (!string.IsNullOrEmpty(assigneeName))
                    {
                        var user = await _userOpService.FindByName(assigneeName);
                        assigneeId = user?.Id;
                    }

                    int? responsibleId = null;
                    if (!string.IsNullOrEmpty(responsibleName))
                    {
                        var user = await _userOpService.FindByName(responsibleName);
                        responsibleId = user?.Id;
                    }

                    await _updateWorkPackageCommand.Execute(wpToAssignId, assigneeId: assigneeId, responsibleId: responsibleId);
                    return new { status = "Usuarios asignados correctamente" };

                case "start_task":
                    var startAssigneeName = GetStringArg(args, "assigneeName");
                    var startResponsibleName = GetStringArg(args, "responsibleName");
                    
                    int? startAssigneeId = null;
                    if (!string.IsNullOrEmpty(startAssigneeName))
                    {
                        var user = await _userOpService.FindByName(startAssigneeName);
                        startAssigneeId = user?.Id;
                    }

                    int? startResponsibleId = null;
                    if (!string.IsNullOrEmpty(startResponsibleName))
                    {
                        var user = await _userOpService.FindByName(startResponsibleName);
                        startResponsibleId = user?.Id;
                    }

                    var startReq = new StarTaskRequest
                    {
                        WorkPackageId = GetOptionalIntArg(args, "workPackageId") ?? 0,
                        ProjectId = GetIntArg(args, "projectId"),
                        StatusId = GetIntArg(args, "statusId"),
                        Name = GetStringArg(args, "name") ?? string.Empty,
                        Description = GetStringArg(args, "description"),
                        ActivityId = GetOptionalIntArg(args, "activityId"),
                        Comment = GetStringArg(args, "comment"),
                        AssigneeId = startAssigneeId,
                        ResponsibleId = startResponsibleId
                    };
                    var task = await _startTaskCommand.Execute(startReq);
                    return new 
                    { 
                        task.WorkPackageId, 
                        task.Name, 
                        status = "Sesión de tiempo iniciada" 
                    };

                case "end_task_session":
                    var endReq = new EndTaskSessionRequest(
                        GetIntArg(args, "workPackageId"),
                        GetIntArg(args, "activityId"),
                        GetStringArg(args, "comment") ?? string.Empty,
                        GetOptionalIntArg(args, "newStatusId")
                    );
                    var endedTask = await _endTaskSessionCommand.Execute(endReq);
                    return new 
                    { 
                        endedTask.WorkPackageId, 
                        endedTask.Name, 
                        status = "Sesión de tiempo finalizada" 
                    };

                default:
                    return new { error = $"Función '{call.Name}' no implementada." };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", call.Name);
            return new { error = ex.Message };
        }
    }

    private int GetIntArg(IDictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement elem) return elem.GetInt32();
            return Convert.ToInt32(val);
        }
        return 0;
    }

    private int? GetOptionalIntArg(IDictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement elem) return elem.GetInt32();
            return Convert.ToInt32(val);
        }
        return null;
    }

    private string? GetStringArg(IDictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var val) && val != null)
        {
            if (val is JsonElement elem) return elem.GetString();
            return val.ToString();
        }
        return null;
    }
}
