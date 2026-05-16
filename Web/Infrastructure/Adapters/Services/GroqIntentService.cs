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
    public class GroqIntentService : IAIService
    {
        private readonly ILogger<GroqIntentService> _logger;
        private readonly GroqSettings _groqSettings;
        private readonly IConversationContextService _conversationContextService;
        private readonly HttpClient _httpClient;
        private readonly IStartTaskCommand _startTaskCommand;
        private readonly IEndTaskSessionCommand _endTaskSessionCommand;
        private readonly IStatusOpService _statusOpService;
        private readonly IUserOpService _userOpService;
        private readonly IActivityOpService _activityOpService;
        private readonly IUpdateWorkPackageCommand _updateWorkPackageCommand;
        private readonly IProjectOpService _projectOpService;
        private readonly ICustomFieldService _customFieldService;
        private readonly IEnumerable<IHeuristicIntentHandler> _heuristicHandlers;

        public GroqIntentService(
            ILogger<GroqIntentService> logger,
            IOptions<GroqSettings> groqSettings,
            IHttpClientFactory httpClientFactory,
            IConversationContextService conversationContextService,
            IStartTaskCommand startTaskCommand,
            IEndTaskSessionCommand endTaskSessionCommand,
            IStatusOpService statusOpService,
            IUserOpService userOpService,
            IActivityOpService activityOpService,
            IUpdateWorkPackageCommand updateWorkPackageCommand,
            IProjectOpService projectOpService,
            ICustomFieldService customFieldService,
            IEnumerable<IHeuristicIntentHandler> heuristicHandlers)
        {
            _logger = logger;
            _groqSettings = groqSettings.Value;
            _conversationContextService = conversationContextService;
            _startTaskCommand = startTaskCommand;
            _endTaskSessionCommand = endTaskSessionCommand;
            _statusOpService = statusOpService;
            _userOpService = userOpService;
            _activityOpService = activityOpService;
            _updateWorkPackageCommand = updateWorkPackageCommand;
            _projectOpService = projectOpService;
            _customFieldService = customFieldService;
            _heuristicHandlers = heuristicHandlers;
            _httpClient = httpClientFactory.CreateClient(_groqSettings.HttpClientName);
        }

        public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
        {
            var context = await _conversationContextService.GetOrCreateAsync(sessionId, ct);

            // CAPA HEURÍSTICA (Patrón Chain of Responsibility)
            foreach (var handler in _heuristicHandlers)
            {
                var result = await handler.HandleAsync(prompt);
                if (result != null)
                {
                    return await SaveContext(context, prompt, result, ct);
                }
            }

            // CAPA LLM
            _logger.LogInformation("Consultando Groq ({Model}) para intención compleja.", _groqSettings.Model);
            if (string.IsNullOrWhiteSpace(_groqSettings.ApiKey))
            {
                return await SaveContext(context, prompt, "⚠️ No se ha configurado la API Key de Groq.", ct);
            }

            string systemPrompt = await GetSystemPromptAsync();

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
                        
                        // Transformar error técnico en amigable
                        string friendlyError = TransformErrorToFriendlyMessage(ex.Message);
                        resultMessages.Add(friendlyError);
                    }
                }
                if (resultMessages.Any()) return await SaveContext(context, prompt, string.Join("\n", resultMessages), ct);
            }
            
            return await SaveContext(context, prompt, aiResponse, ct);
        }

        private string TransformErrorToFriendlyMessage(string technicalError)
        {
            if (technicalError.Contains("422"))
            {
                var sb = new StringBuilder("⚠️ **Faltan datos obligatorios para completar la acción:**\n\n");
                bool foundSpecific = false;

                if (technicalError.Contains("customField3") || technicalError.Contains("Area") || technicalError.Contains("Área"))
                {
                    sb.AppendLine("- 🏢 **Área:** No puede estar en blanco.");
                    foundSpecific = true;
                }
                if (technicalError.Contains("customField5") || technicalError.Contains("Modulo") || technicalError.Contains("Módulo"))
                {
                    sb.AppendLine("- 🧩 **Módulo:** No puede estar en blanco.");
                    foundSpecific = true;
                }
                if (technicalError.Contains("subject") || technicalError.Contains("nombre"))
                {
                    sb.AppendLine("- 📝 **Nombre/Asunto:** Es obligatorio.");
                    foundSpecific = true;
                }

                if (!foundSpecific) return $"⚠️ No se pudo procesar la solicitud en OpenProject (Error 422). Verifica que todos los campos obligatorios estén presentes.";

                sb.AppendLine("\n💡 *Puedes decir algo como: 'Usa el área Soporte y el módulo Nómina'*.");
                return sb.ToString();
            }

            if (technicalError.Contains("403")) return "🚫 No tienes permisos suficientes en OpenProject para realizar esta acción.";
            if (technicalError.Contains("404")) return "🔍 No se encontró el recurso solicitado (tarea, proyecto o usuario).";

            return $"❌ Hubo un error: {technicalError}";
        }

        private async Task<string> GetSystemPromptAsync()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infrastructure", "Config", "Prompts", "GroqSystemPrompt.md");
                if (!File.Exists(path))
                {
                    path = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Config", "Prompts", "GroqSystemPrompt.md");
                }
                
                if (File.Exists(path)) return await File.ReadAllTextAsync(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer el system prompt desde archivo. Usando fallback.");
            }

            return "Eres un asistente de gestión de proyectos.";
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

            async Task<(int? id, string? err)> ResolveProject(string key)
            {
                string name = GetStr(p, key);
                if (string.IsNullOrEmpty(name)) return (null, null);
                var project = await _projectOpService.FindByName(name);
                return project == null ? (null, $"⚠️ No se encontró un proyecto llamado '{name}'.") : (project.Id, null);
            }

            async Task<(int? id, string? err)> ResolveArea(string key)
            {
                string name = GetStr(p, key);
                if (string.IsNullOrEmpty(name)) return (null, null);
                var area = await _customFieldService.FindAreaByName(name);
                return area == null ? (null, $"⚠️ No se encontró un área llamada '{name}'.") : (area.Id, null);
            }

            async Task<(int? id, string? err)> ResolveModule(string key)
            {
                string name = GetStr(p, key);
                if (string.IsNullOrEmpty(name)) return (null, null);
                var module = await _customFieldService.FindModuleByName(name);
                return module == null ? (null, $"⚠️ No se encontró un módulo llamado '{name}'.") : (module.Id, null);
            }

            switch (action.Action)
            {
                case "start_task":
                    var (statusId, sErr) = await ResolveStatus("statusName");
                    if (sErr != null) return sErr;
                    var (actId, aErr) = await ResolveActivity("activityName");
                    if (aErr != null) return aErr;
                    var (areaId, arErr) = await ResolveArea("areaName");
                    if (arErr != null) return arErr;
                    var (modId, mErr) = await ResolveModule("moduleName");
                    if (mErr != null) return mErr;

                    int finalProjectId = GetInt(p, "projectId");
                    if (finalProjectId <= 0)
                    {
                        var (resProjectId, pErr) = await ResolveProject("projectName");
                        if (pErr != null) return pErr;
                        finalProjectId = resProjectId ?? 0;
                    }

                    if (finalProjectId <= 0) return "⚠️ Debes especificar un proyecto válido (ID o nombre).";

                    var newTask = await _startTaskCommand.Execute(new StarTaskRequest
                    {
                        ProjectId = finalProjectId,
                        StatusId = statusId ?? 0,
                        Name = GetStr(p, "name"),
                        WorkPackageId = wpId,
                        AssigneeId = (await _userOpService.FindByName(GetStr(p, "assigneeName")))?.Id,
                        ResponsibleId = (await _userOpService.FindByName(GetStr(p, "responsibleName")))?.Id,
                        ActivityId = actId,
                        Comment = GetStr(p, "comment"),
                        StartDate = GetStr(p, "startDate"),
                        DueDate = GetStr(p, "dueDate"),
                        Area = areaId?.ToString(),
                        Module = modId?.ToString()
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
