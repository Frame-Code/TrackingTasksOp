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
/// Implementación del servicio de intención que utiliza el modelo Gemini de Google, 
/// integrando Function Calling para ejecutar acciones en el sistema.
/// </summary>
public class GeminiIntentService : IGeminiIntentService
{
    private readonly ILogger<GeminiIntentService> _logger;
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

    public GeminiIntentService(
        ILogger<GeminiIntentService> logger,
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

        _generativeAIClient = new Client(
            project: _geminiSettings.ProjectId, 
            location: _geminiSettings.Location, 
            vertexAI: true);
    }

    public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
    {
        var conversationContext = await _conversationContextService.GetOrCreateAsync(sessionId, ct);
        conversationContext.AddUserMessage(prompt);

        try
        {
            _logger.LogInformation("Processing Gemini intent for session {SessionId}", sessionId);

            var tools = new List<Tool>
            {
                new Tool
                {
                    FunctionDeclarations = new List<FunctionDeclaration>
                    {
                        new() {
                            Name = "list_projects",
                            Description = "Obtiene la lista de proyectos disponibles con sus IDs y nombres."
                        },
                        new() {
                            Name = "list_users",
                            Description = "Obtiene la lista de usuarios registrados en el sistema para poder asignar tareas."
                        },
                        new() {
                            Name = "list_work_packages",
                            Description = "Lista los paquetes de trabajo (tareas) de un proyecto específico.",
                            Parameters = new Schema
                            {
                                Type = Google.GenAI.Types.Type.Object,
                                Properties = new Dictionary<string, Schema>
                                {
                                    ["projectId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID del proyecto" }
                                },
                                Required = new List<string> { "projectId" }
                            }
                        },
                        new() {
                            Name = "list_statuses",
                            Description = "Obtiene los estados posibles para asignar a una tarea."
                        },
                        new() {
                            Name = "list_activities",
                            Description = "Lista las actividades permitidas para registrar tiempo en una tarea. Usa esta función si el usuario menciona una actividad por nombre (ej. 'desarrollo') para obtener su ID.",
                            Parameters = new Schema
                            {
                                Type = Google.GenAI.Types.Type.Object,
                                Properties = new Dictionary<string, Schema>
                                {
                                    ["workPackageId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID de la tarea" }
                                },
                                Required = new List<string> { "workPackageId" }
                            }
                        },
                        new() {
                            Name = "start_task",
                            Description = "Inicia el seguimiento de tiempo para una tarea. Si el usuario da un nombre de actividad o de un usuario, primero busca sus IDs. Si la tarea no tiene ID, se crea automáticamente.",
                            Parameters = new Schema
                            {
                                Type = Google.GenAI.Types.Type.Object,
                                Properties = new Dictionary<string, Schema>
                                {
                                    ["workPackageId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID de la tarea (opcional)" },
                                    ["projectId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID del proyecto" },
                                    ["statusId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID del estado" },
                                    ["name"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Nombre de la tarea" },
                                    ["description"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Descripción opcional" },
                                    ["activityId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID numérico de la actividad (opcional)" },
                                    ["assigneeName"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Nombre del usuario asignado (opcional)" },
                                    ["responsibleName"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Nombre del responsable (opcional)" },
                                    ["comment"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Comentario opcional" }
                                },
                                Required = new List<string> { "projectId", "statusId", "name" }
                            }
                        },
                        new() {
                            Name = "assign_user_to_task",
                            Description = "Asigna un usuario como asignado o responsable a una tarea existente.",
                            Parameters = new Schema
                            {
                                Type = Google.GenAI.Types.Type.Object,
                                Properties = new Dictionary<string, Schema>
                                {
                                    ["workPackageId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID de la tarea" },
                                    ["assigneeName"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Nombre del usuario asignado (opcional)" },
                                    ["responsibleName"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Nombre del responsable (opcional)" }
                                },
                                Required = new List<string> { "workPackageId" }
                            }
                        },
                        new() {
                            Name = "end_task_session",
                            Description = "Finaliza la sesión actual de seguimiento de tiempo de una tarea y sube las horas a OpenProject. Opcionalmente puede cambiar el estado de la tarea (ej. para cerrarla).",
                            Parameters = new Schema
                            {
                                Type = Google.GenAI.Types.Type.Object,
                                Properties = new Dictionary<string, Schema>
                                {
                                    ["workPackageId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID de la tarea" },
                                    ["activityId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID de la actividad realizada" },
                                    ["comment"] = new() { Type = Google.GenAI.Types.Type.String, Description = "Comentario sobre el trabajo realizado" },
                                    ["newStatusId"] = new() { Type = Google.GenAI.Types.Type.Integer, Description = "ID del nuevo estado (ej. ID de 'Closed')" }
                                },
                                Required = new List<string> { "workPackageId", "activityId", "comment" }
                            }
                        }
                    }
                }
            };

            var config = new GenerateContentConfig
            {
                Temperature = 0.1f,
                Tools = tools,
                SystemInstruction = new Content
                {
                    Parts = new List<Part>
                    {
                        new Part { Text = "Eres un asistente experto en gestión de tareas para OpenProject. " +
                                         "Tu objetivo es ser lo más proactivo y eficiente posible. " +
                                         "REGLAS CRÍTICAS: " +
                                         "1. NUNCA pidas permiso para usar una herramienta si tienes los datos necesarios. " +
                                         "2. Si el usuario menciona una actividad o un usuario por nombre, llama AUTOMÁTICAMENTE a las funciones de listado para buscar sus IDs. " +
                                         "3. Si necesitas el ID de una tarea o proyecto que no tienes en el mensaje actual pero crees que está en el historial, búscalo tú mismo. " +
                                         "4. Si una operación requiere varios pasos (ej. buscar usuario -> asignar -> empezar trackeo), ejecútalos todos en secuencia sin preguntar entre pasos." }
                    }
                }
            };
            
            var contents = conversationContext.History.Select(item =>
                new Content { Role = item.Type == "ttm" ? "model" : "user", Parts = new List<Part> { new Part { Text = item.Content } } }
            ).ToList();
            
            bool finalResponseReached = false;
            string finalResult = string.Empty;

            while (!finalResponseReached)
            {
                GenerateContentResponse response;
                try 
                {
                    response = await _generativeAIClient.Models.GenerateContentAsync(
                        model: _geminiSettings.Model,
                        contents: contents,
                        config: config,
                        cancellationToken: ct);
                }
                catch (ClientError ex)
                {
                    _logger.LogError(ex, "Gemini ClientError during GenerateContent: {Message}", ex.Message);
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

                // Importante: Añadir el mensaje del modelo al historial antes de procesar llamadas
                contents.Add(candidateContent);

                // Filtrar solo las partes que son llamadas a funciones
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
                        
                        // Normalizar el resultado a un Dictionary<string, object> compatible
                        Dictionary<string, object> normalizedResult = NormalizeResult(result);
                        
                        responseParts.Add(new Part
                        {
                            FunctionResponse = new FunctionResponse
                            {
                                Name = call.Name,
                                Response = normalizedResult
                            }
                        });
                    }

                    // Las respuestas DEBEN ir en un mensaje con el rol "function"
                    contents.Add(new Content { Role = "function", Parts = responseParts });
                    
                    // El bucle continuará para que el modelo procese los resultados de las funciones
                }
                else
                {
                    // Si no hay llamadas a funciones, es la respuesta final
                    finalResult = response.Text ?? "Operación completada.";
                    finalResponseReached = true;
                }
            }

            conversationContext.AddTtmMessage(finalResult);
            await _conversationContextService.SaveAsync(conversationContext, ct);

            return finalResult;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing Gemini intent for session {SessionId}", sessionId);
            return $"Error técnico: {ex.Message}. Intenta simplificar tu solicitud.";
        }
    }

    /// <summary>
    /// Normaliza cualquier objeto a un diccionario compatible con la SDK de Gemini.
    /// Gemini requiere que la respuesta de una función sea un OBJETO JSON {}.
    /// </summary>
    private Dictionary<string, object> NormalizeResult(object result)
    {
        try
        {
            // Si el resultado es una lista, la envolvemos en un objeto porque Gemini no acepta arrays raíz
            if (result is System.Collections.IEnumerable && result is not string && result is not IDictionary<string, object>)
            {
                return new Dictionary<string, object> { ["elements"] = result };
            }

            // Si ya es un diccionario, intentamos usarlo directamente
            if (result is Dictionary<string, object> dict) return dict;

            // Para otros objetos, serializamos y deserializamos para asegurar un formato de diccionario limpio
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
                    return wps.Select(w => new { w.Id, w.Subject, w.Links.Status.Title }).ToList();

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
                        status = "Sesión de tiempo iniciada y tarea configurada correctamente" 
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
                        status = "Sesión de tiempo finalizada y horas subidas a OpenProject" 
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
        throw new ArgumentException($"Missing required argument: {key}");
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

