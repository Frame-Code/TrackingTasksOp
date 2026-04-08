namespace Web.Infrastructure.Adapters.Services
{
    using Application.Dto.ListWorkPackages;
    using Application.Dto.Tasks;
    using Application.Ports.Services;
    using Application.Ports.UseCases.Tasks;
    using Application.Ports.UseCases.WorkPackages;
    using global::Web.Infrastructure.Config.Settings;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;


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

        private const string GroqApiUrl = "https://api.groq.com/openai/v1/chat/completions";

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

            _httpClient = httpClientFactory.CreateClient("GroqClient");
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _groqSettings.ApiKey);
        }

        public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
        {
            var context = await _conversationContextService.GetOrCreateAsync(sessionId, ct);
            string lowerPrompt = prompt.ToLowerInvariant().Trim();

            // ==============================================================================
            // 1. CAPA HEURÍSTICA (respuestas rápidas sin gastar tokens)
            // ==============================================================================

            if (lowerPrompt == "proyectos" || lowerPrompt.Contains("listar proyectos") || lowerPrompt.Contains("mis proyectos"))
            {
                var projects = await _projectOpService.Lists();
                string resp = "📋 **Tus Proyectos Disponibles:**\n\n" +
                              string.Join("\n", projects.Select(p => $"- **{p.Name}** (ID: {p.Id})"));
                return await SaveContext(context, prompt, resp, ct);
            }

            if (lowerPrompt == "tareas" || lowerPrompt.Contains("mis tareas") ||
                lowerPrompt.Contains("qué tengo pendiente") || lowerPrompt.Contains("que tengo pendiente"))
            {
                var wps = await _listsWorkPackagesCommand.Execute(new ListsWorkPackagesRequest(null, 0, 50));
                if (!wps.Any())
                    return await SaveContext(context, prompt, "✅ No tienes tareas pendientes.", ct);

                var builder = new StringBuilder("📝 **Tus Tareas Pendientes:**\n\n");
                foreach (var g in wps.GroupBy(w => w.Links?.Project?.Title ?? "Otros"))
                {
                    builder.AppendLine($"📁 **{g.Key}**");
                    foreach (var t in g)
                        builder.AppendLine($"- **#{t.Id}**: {t.Subject} *({t.Links?.Status?.Title})*");
                    builder.AppendLine();
                }
                return await SaveContext(context, prompt, builder.ToString().TrimEnd(), ct);
            }

            if (lowerPrompt.Contains("mostrar usuarios") || lowerPrompt.Contains("listar usuarios") || lowerPrompt == "usuarios")
            {
                var users = await _userOpService.Lists();
                string resp = "👥 **Usuarios:**\n\n" +
                              string.Join("\n", users.Select(u => $"- {u.Name} (ID: {u.Id})"));
                return await SaveContext(context, prompt, resp, ct);
            }

            if (lowerPrompt.Contains("Mostrar estados disponibles") || lowerPrompt.Contains("Ver estados disponibles") || lowerPrompt == "estados")
            {
                var states = await _statusOpService.Lists();
                string resp = "👥 **Estados disponibles:**\n\n" +
                              string.Join("\n", states.Select(u => $"- {u.Name} (ID: {u.Id})"));
                return await SaveContext(context, prompt, resp, ct);
            }

            // ==============================================================================
            // 2. CAPA LLM — GROQ
            // ==============================================================================
            _logger.LogInformation("Consultando Groq ({Model}) para intención compleja.", _groqSettings.Model);

            // Validar que la API key esté configurada
            if (string.IsNullOrWhiteSpace(_groqSettings.ApiKey))
            {
                const string noKeyMsg = "⚠️ No se ha configurado la API Key de Groq. " +
                                        "Agrega tu key en appsettings.Development.json bajo 'Groq:ApiKey'.";
                return await SaveContext(context, prompt, noKeyMsg, ct);
            }

            var systemPrompt = @"Eres un asistente de TrackingTasksOp para gestión de proyectos en OpenProject.

                ACCIONES DISPONIBLES (SOLO ESTAS 3, NINGUNA MÁS):

                1. start_task — Crea tarea, asigna usuarios y trackea tiempo TODO EN UNO:
                { ""action"": ""start_task"", ""params"": { ""projectId"": int, ""statusId"": int, ""name"": string, ""workPackageId"": int, ""assigneeName"": string, ""responsibleName"": string, ""description"": string, ""activityId"": int, ""comment"": string } }

                2. assign_user_to_task — Reasignar assignee O responsable en tarea existente:
                { ""action"": ""assign_user_to_task"", ""params"": { ""workPackageId"": int, ""assigneeName"": string, ""responsibleName"": string } }

                3. end_task_session — Finaliza tracking de tiempo:
                { ""action"": ""end_task_session"", ""params"": { ""workPackageId"": int, ""activityId"": int, ""comment"": string } }

                REGLAS ABSOLUTAS:
                - PROHIBIDO usar acciones distintas a las 3 de arriba
                - PROHIBIDO usar 'assign_responsible_for_task', 'start_task_session', 'update_task' u otras inventadas
                - Para asignar responsable en tarea existente: usar assign_user_to_task con responsibleName
                - SIEMPRE usar nombres de usuario (string), NUNCA IDs numéricos para usuarios
                - SIN backticks, SIN markdown, SIN bloques ```json, SIN texto adicional
                - Campos no mencionados: omitirlos o poner null

                EJEMPLOS CORRECTOS:

                Usuario: 'Asigna a Stin Sanchez como responsable de la tarea 50'
                Respuesta CORRECTA:
                { ""action"": ""assign_user_to_task"", ""params"": { ""workPackageId"": 50, ""assigneeName"": null, ""responsibleName"": ""Stin Sanchez"" } }

                Usuario: 'Crea tarea Integración en proyecto 3, estado 4, asignala a Juan, responsable Maria'
                Respuesta CORRECTA:
                { ""action"": ""start_task"", ""params"": { ""projectId"": 3, ""statusId"": 4, ""name"": ""Integración"", ""workPackageId"": 0, ""assigneeName"": ""Juan"", ""responsibleName"": ""Maria"", ""description"": null, ""activityId"": null, ""comment"": null } }

                Usuario: 'lista mis tareas'
                Respuesta CORRECTA: (texto normal en español, sin JSON)";

            var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

            // Agregar historial de conversación
            foreach (var h in context.History)
                messages.Add(new { role = h.Type == "user" ? "user" : "assistant", content = h.Content });

            // Mensaje actual
            messages.Add(new { role = "user", content = prompt });

            var requestBody = new
            {
                model = _groqSettings.Model,
                messages,
                temperature = _groqSettings.Temperature,
                max_tokens = 1024
            };

            string aiResponse;
            try
            {
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var httpResponse = await _httpClient.PostAsync(GroqApiUrl, jsonContent, ct);
                httpResponse.EnsureSuccessStatusCode();

                var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(responseJson);

                aiResponse = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error llamando a Groq API");
                string errMsg = ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "⚠️ API Key de Groq inválida. Verifica tu configuración en appsettings.Development.json."
                    : "⚠️ Error al conectar con Groq. Intenta nuevamente.";
                return await SaveContext(context, prompt, errMsg, ct);
            }

            // Intentar parsear si la IA devolvió un JSON de acción
            // Limpiar backticks y bloques de código markdown
            string trimmed = aiResponse.Trim()
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            if (trimmed.Contains("\"action\""))
            {
                // Separar múltiples JSONs — el modelo los pega uno tras otro
                var jsonBlocks = ExtractJsonBlocks(trimmed);
                var resultMessages = new List<string>();

                int? lastCreatedWpId = null;
                foreach (var block in jsonBlocks)
                {
                    try
                    {
                        var actionData = JsonSerializer.Deserialize<GroqAction>(block,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (actionData != null)
                        {
                            var msg = await ExecuteAction(actionData, lastCreatedWpId);

                            // Si fue start_task, extraer el ID para las siguientes acciones
                            if (actionData.Action == "start_task" && msg.Contains("ID:"))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(msg, @"ID:\s*(\d+)");
                                if (match.Success && int.TryParse(match.Groups[1].Value, out int newId))
                                    lastCreatedWpId = newId;
                            }

                            if (!string.IsNullOrEmpty(msg))
                                resultMessages.Add(msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error ejecutando acción");
                    }
                }

                if (resultMessages.Any())
                {
                    string finalMsg = string.Join("\n", resultMessages);
                    return await SaveContext(context, prompt, finalMsg, ct);
                }
            }

            // ── VALIDACIÓN ANTI-ALUCINACIÓN ──────────────────────────────────────
            // El LLM respondió texto pero suena a confirmación de acción → advertir
            string[] falseConfirmationKeywords = ["actualizada", "asignado", "asignada",
                "creada", "correctamente", "finalizada", "registrada", "completado"];

            bool soundsLikeAction = falseConfirmationKeywords
                .Any(k => aiResponse.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (soundsLikeAction)
            {
                string warningMsg = "⚠️ No pude ejecutar esa acción directamente. " +
                    "Por favor sé más específico, por ejemplo:\n\n" +
                    "- *'Asigna responsable Stin Sanchez a la tarea 50'*\n" +
                    "- *'Crea tarea X en proyecto 3 con estado 4'*\n" +
                    "- *'Finaliza el tiempo de la tarea 50'*";
                return await SaveContext(context, prompt, warningMsg, ct);
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

                int depth = 0;
                int end = start;
                for (int j = start; j < text.Length; j++)
                {
                    if (text[j] == '{') depth++;
                    else if (text[j] == '}') depth--;

                    if (depth == 0)
                    {
                        end = j;
                        break;
                    }
                }

                string block = text[start..(end + 1)];
                if (block.Contains("\"action\""))
                    blocks.Add(block);

                i = end + 1;
            }
            return blocks;
        }

        private async Task<string> ExecuteAction(GroqAction action, int? contextWorkPackageId = null)
        {
            var p = action.Params;

            int wpId = GetInt(p, "workPackageId");
            if (wpId == 0 && contextWorkPackageId.HasValue)
                wpId = contextWorkPackageId.Value;

            switch (action.Action)
            {
                case "start_task":
                    // Resolver assignee
                    int? assigneeId = null;
                    string assigneeName = GetStr(p, "assigneeName");
                    if (!string.IsNullOrEmpty(assigneeName))
                    {
                        var assignee = await _userOpService.FindByName(assigneeName);
                        assigneeId = assignee?.Id;
                    }

                    // Resolver responsible
                    int? responsibleId = null;
                    string responsibleName = GetStr(p, "responsibleName");
                    if (!string.IsNullOrEmpty(responsibleName))
                    {
                        var responsible = await _userOpService.FindByName(responsibleName);
                        responsibleId = responsible?.Id;
                    }

                    var newTask = await _startTaskCommand.Execute(new StarTaskRequest
                    {
                        ProjectId = GetInt(p, "projectId"),
                        StatusId = GetInt(p, "statusId"),
                        Name = GetStr(p, "name"),
                        WorkPackageId = wpId,
                        Description = GetStr(p, "description"),
                        ActivityId = GetNullableInt(p, "activityId"),
                        Comment = GetStr(p, "comment"),
                        AssigneeId = assigneeId,
                        ResponsibleId = responsibleId
                    });

                    var resultMsg = $"🚀 Tarea **{GetStr(p, "name")}** creada y tiempo activado. (ID: {newTask?.WorkPackageId})";
                    if (!string.IsNullOrEmpty(assigneeName)) resultMsg += $"\n👤 Asignado a: **{assigneeName}**";
                    if (!string.IsNullOrEmpty(responsibleName)) resultMsg += $"\n👤 Responsable: **{responsibleName}**";
                    return resultMsg;

                case "assign_user_to_task":
                    int? aId = null;
                    string aName = GetStr(p, "assigneeName");
                    if (!string.IsNullOrEmpty(aName))
                    {
                        var u = await _userOpService.FindByName(aName);
                        aId = u?.Id;
                    }

                    int? rId = null;
                    string rName = GetStr(p, "responsibleName");
                    if (!string.IsNullOrEmpty(rName))
                    {
                        var u = await _userOpService.FindByName(rName);
                        rId = u?.Id;
                    }

                    await _updateWorkPackageCommand.Execute(wpId, assigneeId: aId, responsibleId: rId);
                    return $"👤 Usuarios asignados correctamente a la tarea #{wpId}.";

                case "end_task_session":
                    await _endTaskSessionCommand.Execute(new EndTaskSessionRequest(
                        wpId,
                        GetInt(p, "activityId"),
                        GetStr(p, "comment"),
                        GetNullableInt(p, "newStatusId")));
                    return "⏹️ Sesión de trabajo finalizada y horas subidas a OpenProject.";

                
                default:
                    _logger.LogWarning("Acción no reconocida ignorada: {Action}", action.Action);
                    // CAMBIAR: en lugar de "" retornar mensaje de error
                    return $"⚠️ Acción *'{action.Action}'* no reconocida. Solo puedes usar: start_task, assign_user_to_task, end_task_session.";
            }
        }

        private static int? GetNullableInt(Dictionary<string, object>? dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out var val) && val != null)
            {
                if (val is JsonElement el)
                    return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;
                if (int.TryParse(val.ToString(), out int result))
                    return result;
            }
            return null;
        }

        private static int GetInt(Dictionary<string, object>? dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out var val))
            {
                if (val is JsonElement el)
                    return el.ValueKind == JsonValueKind.Number ? el.GetInt32() : 0;
                return Convert.ToInt32(val);
            }
            return 0;
        }

        private static string GetStr(Dictionary<string, object>? dict, string key) =>
            (dict != null && dict.TryGetValue(key, out var val)) ? val?.ToString() ?? "" : "";

        private async Task<string> SaveContext(
            Application.Dto.Conversation.ConversationContext context,
            string prompt, string response, CancellationToken ct)
        {
            context.AddUserMessage(prompt);
            context.AddTtmMessage(response);
            await _conversationContextService.SaveAsync(context, ct);
            return response;
        }

        private class GroqAction
        {
            public string Action { get; set; } = "";
            public Dictionary<string, object>? Params { get; set; }
        }
    }
}
