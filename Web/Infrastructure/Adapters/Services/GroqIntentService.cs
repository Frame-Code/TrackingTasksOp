using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Dto.ListWorkPackages;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Adapters.Services
{
    public class GroqIntentService : IGeminiIntentService
    {
        private readonly ILogger<GroqIntentService> _logger;
        private readonly GroqSettings _groqSettings;
        private readonly IConversationContextService _conversationContextService;
        private readonly HttpClient _httpClient;
        private readonly IStartTaskCommand _startTaskCommand;
        private readonly IEndTaskSessionCommand _endTaskSessionCommand;
        private readonly IListsWorkPackagesCommand _listsWorkPackagesCommand;
        private readonly IProjectOpService _projectOpService;
        private readonly IStatusOpService _statusOpService;
        private readonly IUserOpService _userOpService;
        private readonly IActivityOpService _activityOpService;
        private readonly IUpdateWorkPackageCommand _updateWorkPackageCommand;

        public GroqIntentService(
            ILogger<GroqIntentService> logger,
            IOptions<GroqSettings> groqSettings,
            IHttpClientFactory httpClientFactory,
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
            _groqSettings = groqSettings.Value;
            _conversationContextService = conversationContextService;
            _startTaskCommand = startTaskCommand;
            _endTaskSessionCommand = endTaskSessionCommand;
            _listsWorkPackagesCommand = listsWorkPackagesCommand;
            _projectOpService = projectOpService;
            _statusOpService = statusOpService;
            _userOpService = userOpService;
            _activityOpService = activityOpService;
            _updateWorkPackageCommand = updateWorkPackageCommand;
            _httpClient = httpClientFactory.CreateClient(_groqSettings.HttpClientName);
        }

        public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
        {
            var context = await _conversationContextService.GetOrCreateAsync(sessionId, ct);
            string lowerPrompt = prompt.ToLowerInvariant().Trim();

            // CAPA HEURÍSTICA
            if (lowerPrompt == "proyectos" || lowerPrompt.Contains("listar proyectos"))
            {
                var projects = await _projectOpService.Lists();
                return await SaveContext(context, prompt, "📋 **Tus Proyectos Disponibles:**\n\n" + string.Join("\n", projects.Select(p => $"- **{p.Name}** (ID: {p.Id})")), ct);
            }
            if (lowerPrompt == "tareas" || lowerPrompt.Contains("mis tareas") || lowerPrompt.Contains("qué tengo pendiente"))
            {
                var wps = await _listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50));
                if (!wps.Any()) return await SaveContext(context, prompt, "✅ No tienes tareas pendientes.", ct);
                var builder = new StringBuilder("📝 **Tus Tareas Pendientes:**\n\n");
                foreach (var g in wps.GroupBy(w => w.Links?.Project?.Title ?? "Otros"))
                {
                    builder.AppendLine($"📁 **{g.Key}**");
                    foreach (var t in g) builder.AppendLine($"- **#{t.Id}**: {t.Subject} *({t.Links?.Status?.Title})*");
                    builder.AppendLine();
                }
                return await SaveContext(context, prompt, builder.ToString().TrimEnd(), ct);
            }
            if (lowerPrompt.Contains("usuarios") || lowerPrompt.Contains("listar usuarios"))
            {
                var users = await _userOpService.Lists();
                return await SaveContext(context, prompt, "👥 **Usuarios:**\n\n" + string.Join("\n", users.Select(u => $"- {u.Name} (ID: {u.Id})")), ct);
            }
            if (lowerPrompt.Contains("estados") || lowerPrompt.Contains("listar estados"))
            {
                var states = await _statusOpService.Lists();
                return await SaveContext(context, prompt, "🗂️ **Estados disponibles:**\n\n" + string.Join("\n", states.Select(u => $"- {u.Name} (ID: {u.Id})")), ct);
            }

            // CAPA LLM
            _logger.LogInformation("Consultando Groq ({Model}) para intención compleja.", _groqSettings.Model);
            if (string.IsNullOrWhiteSpace(_groqSettings.ApiKey))
            {
                return await SaveContext(context, prompt, "⚠️ No se ha configurado la API Key de Groq.", ct);
            }

            var systemPrompt = @"Eres un asistente de TrackingTasksOp para gestión de proyectos en OpenProject.

                ACCIONES DISPONIBLES:
                1. start_task: { ""action"": ""start_task"", ""params"": { ""projectId"": int, ""statusName"": string, ""name"": string, ""assigneeName"": string, ""responsibleName"": string, ""activityName"": string, ""comment"": string } }
                2. assign_user_to_task: { ""action"": ""assign_user_to_task"", ""params"": { ""workPackageId"": int, ""statusName"": string, ""assigneeName"": string, ""responsibleName"": string } }
                3. end_task_session: { ""action"": ""end_task_session"", ""params"": { ""workPackageId"": int, ""activityName"": string, ""comment"": string, ""newStatusName"": string } }

                REGLAS ABSOLUTAS:
                - USA NOMBRES para estados, actividades y usuarios (ej: ""statusName"": ""En curso"").
                - Para IDs de proyecto, SÍ debes usar números (ej: ""projectId"": 3).
                - NO uses markdown, ```json, ni texto adicional. Solo el JSON.
                - Si un campo no se menciona, omítelo o ponle null.

                EJEMPLOS:
                Usuario: 'Crea tarea ""Revisar despliegue"" en proyecto 3, estado ""En curso"".'
                Respuesta: { ""action"": ""start_task"", ""params"": { ""projectId"": 3, ""statusName"": ""En curso"", ""name"": ""Revisar despliegue"" } }

                Usuario: 'Finaliza la tarea 54 con actividad ""Desarrollo"" y ponla en estado ""Cerrado"". Comentario: ""deploy ok"".'
                Respuesta: { ""action"": ""end_task_session"", ""params"": { ""workPackageId"": 54, ""activityName"": ""Desarrollo"", ""comment"": ""deploy ok"", ""newStatusName"": ""Cerrado"" } }
                
                Usuario: 'Pon la tarea 125 en estado ""En espera"" y asigna a ""Laura Campos"" como encargada.'
                Respuesta: { ""action"": ""assign_user_to_task"", ""params"": { ""workPackageId"": 125, ""statusName"": ""En espera"", ""assigneeName"": ""Laura Campos"" } }";

            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            context.History.ForEach(h => messages.Add(new { role = h.Type == "user" ? "user" : "assistant", content = h.Content }));
            messages.Add(new { role = "user", content = prompt });

            var requestBody = new { model = _groqSettings.Model, messages, temperature = _groqSettings.Temperature, max_tokens = 1024 };
            string aiResponse;
            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var httpResponse = await _httpClient.PostAsync(_groqSettings.BaseUrl, jsonContent, ct);
                httpResponse.EnsureSuccessStatusCode();
                var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
                aiResponse = JsonDocument.Parse(responseJson).RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error llamando a Groq API");
                return await SaveContext(context, prompt, "⚠️ Error al conectar con Groq.", ct);
            }
            
            string trimmed = aiResponse.Trim().Replace("```json", "").Replace("```", "").Trim();
            if (trimmed.Contains("\"action\""))
            {
                var resultMessages = new List<string>();
                int? lastCreatedWpId = null;
                foreach (var block in ExtractJsonBlocks(trimmed))
                {
                    try
                    {
                        var actionData = JsonSerializer.Deserialize<GroqAction>(block, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (actionData != null)
                        {
                            var msg = await ExecuteAction(actionData, lastCreatedWpId);
                            if (actionData.Action == "start_task" && msg.Contains("ID:"))
                            {
                                var match = Regex.Match(msg, @"ID:\s*(\d+)");
                                if (match.Success && int.TryParse(match.Groups[1].Value, out int newId)) lastCreatedWpId = newId;
                            }
                            if (!string.IsNullOrEmpty(msg)) resultMessages.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error ejecutando acción: {Message}", ex.Message);
                        resultMessages.Add($"❌ Hubo un error: {ex.Message}");
                    }
                }
                if (resultMessages.Any()) return await SaveContext(context, prompt, string.Join("\n", resultMessages), ct);
            }
            
            return await SaveContext(context, prompt, aiResponse, ct);
        }

        private static List<string> ExtractJsonBlocks(string text)
        {
            var blocks = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                int start = text.IndexOf('{', i);
                if (start == -1) break;
                int depth = 1, end = start + 1;
                while (end < text.Length && depth > 0)
                {
                    if (text[end] == '{') depth++;
                    else if (text[end] == '}') depth--;
                    end++;
                }
                if (depth == 0)
                {
                    string block = text[start..end];
                    if (block.Contains("\"action\"")) blocks.Add(block);
                    i = end;
                } else { i = start + 1; }
            }
            return blocks;
        }

        private async Task<string> ExecuteAction(GroqAction action, int? contextWorkPackageId = null)
        {
            var p = action.Params;
            int wpId = GetInt(p, "workPackageId");
            if (wpId == 0 && contextWorkPackageId.HasValue) wpId = contextWorkPackageId.Value;

            async Task<(int? id, string? err)> ResolveStatus(string key)
            {
                string name = GetStr(p, key);
                if (string.IsNullOrEmpty(name)) return (null, null);
                var status = await _statusOpService.FindByNameAsync(name);
                return status == null ? (null, $"⚠️ No se encontró un estado llamado '{name}'.") : (status.Id, null);
            }
            
            async Task<(int? id, string? err)> ResolveActivity(string key)
            {
                string name = GetStr(p, key);
                if (string.IsNullOrEmpty(name)) return (null, null);
                if (wpId <= 0) return (null, "⚠️ Para asignar una actividad, primero debes especificar un ID de tarea válido.");
                var activity = await _activityOpService.FindByNameAsync(name, wpId);
                return activity == null ? (null, $"⚠️ La actividad '{name}' no es válida para la tarea #{wpId}.") : (activity.Id, null);
            }

            switch (action.Action)
            {
                case "start_task":
                    var (statusId, sErr) = await ResolveStatus("statusName");
                    if (sErr != null) return sErr;
                    var (actId, aErr) = await ResolveActivity("activityName");
                    if (aErr != null) return aErr;

                    var newTask = await _startTaskCommand.Execute(new StarTaskRequest
                    {
                        ProjectId = GetInt(p, "projectId"),
                        StatusId = statusId ?? 0,
                        Name = GetStr(p, "name"),
                        WorkPackageId = wpId,
                        AssigneeId = (await _userOpService.FindByName(GetStr(p, "assigneeName")))?.Id,
                        ResponsibleId = (await _userOpService.FindByName(GetStr(p, "responsibleName")))?.Id,
                        ActivityId = actId,
                        Comment = GetStr(p, "comment")
                    });
                    return $"🚀 Tarea **{newTask.Name}** creada y tiempo activado. (ID: {newTask.WorkPackageId})";

                case "assign_user_to_task":
                    var (updStatusId, updSErr) = await ResolveStatus("statusName");
                    if (updSErr != null) return updSErr;

                    await _updateWorkPackageCommand.Execute(wpId, 
                        statusId: updStatusId,
                        assigneeId: (await _userOpService.FindByName(GetStr(p, "assigneeName")))?.Id, 
                        responsibleId: (await _userOpService.FindByName(GetStr(p, "responsibleName")))?.Id);
                    return $"✅ Tarea #{wpId} actualizada.";

                case "end_task_session":
                    var (endStatusId, endSErr) = await ResolveStatus("newStatusName");
                    if (endSErr != null) return endSErr;
                    var (endActId, endAErr) = await ResolveActivity("activityName");
                    if (endAErr != null) return endAErr;
                    
                    if (!endActId.HasValue) return "⚠️ Para finalizar una sesión, debes especificar el nombre de la actividad (ej: 'con actividad Desarrollo').";

                    await _endTaskSessionCommand.Execute(new EndTaskSessionRequest(wpId, endActId.Value, GetStr(p, "comment"), endStatusId));
                    return "⏹️ Sesión de trabajo finalizada y horas subidas a OpenProject.";

                default:
                    return $"⚠️ Acción *'{action.Action}'* no reconocida.";
            }
        }
        
        private static int? GetNullableInt(Dictionary<string, object>? dict, string key) => (dict != null && dict.TryGetValue(key, out var val) && val is JsonElement el && el.ValueKind == JsonValueKind.Number) ? el.GetInt32() : null;
        private static int GetInt(Dictionary<string, object>? dict, string key) => GetNullableInt(dict, key) ?? 0;
        private static string GetStr(Dictionary<string, object>? dict, string key) => (dict != null && dict.TryGetValue(key, out var val)) ? val?.ToString() ?? "" : "";
        
        private async Task<string> SaveContext(Application.Dto.Conversation.ConversationContext context, string prompt, string response, CancellationToken ct)
        {
            context.AddUserMessage(prompt);
            context.AddTtmMessage(response);
            await _conversationContextService.SaveAsync(context, ct);
            return response;
        }

        private class GroqAction { public string Action { get; set; } = ""; public Dictionary<string, object>? Params { get; set; } }
    }
}
