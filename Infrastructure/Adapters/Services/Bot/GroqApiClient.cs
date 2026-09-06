using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Dto.Conversation;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Adapters.Services.Bot;

public class GroqApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GroqSettings> groqSettings,
    GroqAuthHeaderProvider authHeaderProvider) : IGroqApiClient
{
    private readonly GroqSettings _groqSettings = groqSettings.Value;
    private HttpClient? _httpClient;

    private HttpClient HttpClient => _httpClient ??= httpClientFactory.CreateClient(KeyedServicesNames.GroqHttpClientName);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_groqSettings.ApiKey);

    /// <summary>
    /// Tope de espera de un reintento. Groq suele pedir segundos, pero si pide minutos es
    /// preferible fallar rápido que dejar al usuario mirando el chat sin señales de vida.
    /// </summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    public async Task<GroqCompletionResult> GetCompletionAsync(ConversationContext context, string prompt, CancellationToken ct = default)
    {
        try
        {
            return await SendAsync(context, prompt, includeTools: true, ct);
        }
        catch (GroqApiException ex) when (ex.Kind == GroqFailureKind.ToolValidation)
        {
            // El modelo intentó llamar como función nativa una acción que en este diseño viaja
            // como JSON embebido en el texto, y Groq rechaza el request entero. Sin el array
            // "tools" no tiene esa opción y responde con el bloque JSON, que BotActionExecutor
            // ya sabe leer. Se pierde el tool calling de create_task/start_task en ese intento,
            // que es mucho mejor que devolver un error.
            return await SendAsync(context, prompt, includeTools: false, ct);
        }
        catch (GroqApiException ex) when (ex.Kind == GroqFailureKind.RateLimited
                                         && ex.RetryAfter is { } delay
                                         && delay <= MaxRetryDelay)
        {
            // El límite del plan es por minuto y el system prompt consume casi todo el cupo, así
            // que dos preguntas seguidas chocan siempre. Esperar lo que Groq indica convierte un
            // error en una respuesta apenas más lenta.
            await Task.Delay(delay, ct);
            return await SendAsync(context, prompt, includeTools: true, ct);
        }
    }

    private async Task<GroqCompletionResult> SendAsync(ConversationContext context, string prompt, bool includeTools, CancellationToken ct)
    {
        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _groqSettings.Model,
            ["messages"] = BuildMessages(context, prompt),
            ["temperature"] = _groqSettings.Temperature,
            ["max_tokens"] = 1024,
            // Específico de los modelos gpt-oss de Groq: limita el razonamiento interno para
            // reducir tokens/latencia (no nos hace falta razonamiento profundo, solo que cumpla
            // las reglas del prompt) y ayuda a no pasarnos del límite de TPM del plan gratuito.
            ["reasoning_effort"] = "low"
        };

        if (includeTools)
            requestBody["tools"] = GroqTools.All;

        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _groqSettings.BaseUrl) { Content = jsonContent };
        // Bearer por request: la key propia del usuario si la configuró, si no la compartida
        // del servidor (sobreescribe el default horneado en el HttpClient nombrado).
        httpRequest.Headers.Authorization = await authHeaderProvider.GetAuthorizationHeaderAsync();
        var httpResponse = await HttpClient.SendAsync(httpRequest, ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorBody = await httpResponse.Content.ReadAsStringAsync(ct);
            throw GroqApiException.FromResponse(httpResponse.StatusCode, errorBody);
        }

        var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

        var result = new GroqCompletionResult();

        if (message.TryGetProperty("tool_calls", out var toolCallsElement) && toolCallsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolCall in toolCallsElement.EnumerateArray())
            {
                var function = toolCall.GetProperty("function");
                result.ToolCalls.Add(new GroqToolCall
                {
                    Id = toolCall.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Name = function.GetProperty("name").GetString() ?? "",
                    ArgumentsJson = function.GetProperty("arguments").GetString() ?? "{}"
                });
            }
        }

        var content = message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
            ? contentEl.GetString() ?? ""
            : "";
        result.Text = CleanResponse(content);

        return result;
    }

    private List<object> BuildMessages(ConversationContext context, string prompt)
    {
        var messages = new List<object> { new { role = "system", content = BuildSystemPrompt() } };
        // Historial para contexto (se limita para no exceder el TPM del modelo configurado).
        // ContentForModel() y no Content: los resultados largos ya se guardaron resumidos para
        // el modelo — arrastrar una lista de 30 tareas cuatro turnos seguidos era lo que
        // reventaba el límite por minuto. El usuario sigue viendo el texto completo.
        foreach (var h in context.History.TakeLast(4))
            messages.Add(new { role = h.Type == "user" ? "user" : "assistant", content = h.ContentForModel() });
        messages.Add(new { role = "user", content = prompt });
        return messages;
    }

    private static string CleanResponse(string res)
    {
        string cleaned = Regex.Replace(res, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    internal static string BuildSystemPrompt()
    {
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        return $@"Eres un ASISTENTE EXPERTO en gestión de proyectos para OpenProject. Ayuda al usuario a gestionar sus tareas de forma eficiente.

        Fecha de hoy: {today} (yyyy-MM-dd). Úsala para resolver expresiones relativas (""hoy"", ""mañana"", etc.) y conviértelas a yyyy-MM-dd.

        REGLAS:
        1. Analiza el pedido y genera los comandos JSON necesarios. Puedes incluir texto antes/después del JSON.
        2. Si falta info crítica (ej. proyecto), pregunta; no inventes datos. Usa NOMBRES (no IDs) para proyectos/estados/usuarios/responsables/asignados. NUNCA le preguntes al usuario ""¿cuál es el ID?"" de algo que ya te dio por nombre: si dice ""proyecto eProduction"" o ""responsable Juan Pérez"", usa exactamente ""projectName"": ""eProduction"" o ""responsibleName"": ""Juan Pérez"" en el JSON tal cual, sin buscar ni pedir ningún número — el sistema resuelve el ID solo, vos NUNCA necesitás saberlo. Si no sabes a quién asignar, usa ""list_project_users"" antes de preguntar.
        3. CREAR ≠ INICIAR. Son dos pasos separados y el usuario controla cada uno. ""create_task"" crea la tarea en OpenProject SIN arrancar el cronómetro. ""start_task"" arranca el cronómetro de una tarea que ya existe. NUNCA llames a ""start_task"" justo después de ""create_task"" salvo que el usuario lo pida explícitamente en el mismo mensaje (ej. ""creá la tarea X y empezá a trabajarla ya""). Tras crear, ofrecé iniciar el seguimiento y esperá su respuesta. ANTES de ""create_task"": si falta info NO crítica con default razonable (fechas, descripción, asignado), NO llames a la función todavía. Responde con texto: qué datos usarás tal cual, cuáles faltan y el default propuesto (ej. ""no indicaste fecha de inicio, usaré hoy {today}""), y pregunta si procedes. Llamá a la función en tu SIGUIENTE respuesta tras confirmación (""sí"", ""dale"", ""confirmo"", ""adelante""), usando valores corregidos si el usuario los da. Si el usuario ya dio todo o dice que no le importan los defaults, llamá a la función directo.
        4. IDENTIFICACIÓN DE workPackageId: si el usuario menciona ""#1134"", ""tarea 1134"", ""ID 1134"", ""work package 1134"", etc., ese número ES el ""workPackageId""; úsalo LITERAL y directo en el JSON. NUNCA digas que no conoces el ID si el usuario lo dio, ni omitas ""workPackageId"" en ese caso. Las acciones sobre tareas existentes (resume_task, pause_task, end_task_session, update_task_status, assign_user_to_task, update_progress, update_task_dates) validan el ID al ejecutarse: genera el JSON de inmediato, sin confirmar el ID ni listar tareas antes.
        5. FILTROS list_tasks: si el usuario no pide filtrar por estado, NO incluyas ""statusName"" (deja ""params"": {{}}). Nunca uses ""All""/""Todos"" como statusName, no son estados válidos.
        6. TRANSICIONES DE ESTADO NO PERMITIDAS: si ""update_task_status"" (o el cambio dentro de ""end_task_session"") falla con un mensaje tipo ""...Desde el estado actual puedes cambiar directamente a: A, B, C"", el estado pedido no es alcanzable en un paso pero sí por pasos intermedios. Explícaselo al usuario y, si A/B/C acerca razonablemente la tarea al objetivo, ofrece generar ""update_task_status"" hacia ese estado intermedio como primer paso (y continuar en mensajes siguientes).
        7. ACCIONES DE SOLO LECTURA (""list_projects"", ""list_project_users"", ""list_tasks"", ""list_statuses""): no modifican datos, NUNCA pidas confirmación ni preguntes ""¿deseas que continúe?"" (la regla 3 aplica solo a ""start_task""). El sistema agrega el resultado real DESPUÉS de ejecutar el JSON con datos de OpenProject, así que NO escribas texto explicativo ni inventes nombres/IDs/resultados: responde ÚNICAMENTE con el bloque JSON.
        8. ASIGNACIÓN A UNO MISMO: si el usuario pide asignarse la tarea a sí mismo (ej. ""asígnamela a mí"", ""ponme como responsable"", ""que sea para mí"", ""asignármela""), usa LITERALMENTE ""yo"" como ""assigneeName"" y/o ""responsibleName"" en el JSON. No adivines el nombre real ni uses ""list_project_users"" para este caso: el sistema resuelve ""yo"" a la cuenta de OpenProject de quien conversa contigo.
        9. ANTES de ""end_task_session"": si el usuario NO especificó a qué estado pasa la tarea ni si actualizar el porcentaje de avance, NO generes el JSON aún. Pregunta solo con texto: a qué estado quiere cambiar la tarea (ofrécele algo razonable según contexto, ej. ""Closed"" o ""Resolved"", o pregúntale directamente), y si quiere actualizar ""percentageDone"" y a qué valor. Genera el JSON de ""end_task_session"" (con ""newStatusName"" si corresponde, y un ""update_progress"" adicional si dio porcentaje) en tu SIGUIENTE respuesta, tras la respuesta del usuario. Si ya dio esos datos o dice que no quiere cambiar nada, genera el JSON directo.
        10. CONFIRMACIÓN DE ACCIONES EJECUTADAS: tu texto SOLO puede decir que una tarea/sesión/acción fue creada, iniciada, finalizada o completada si en ESA MISMA respuesta incluís el bloque JSON completo y válido de esa acción — el sistema NO ejecuta nada sin el JSON, sin importar lo que diga tu texto. Si todavía no vas a incluir el JSON (ej. porque falta confirmar algo), tu texto NUNCA puede sonar a que ya se hizo. Cuando SÍ generás el bloque JSON: nunca digas ""voy a crear la tarea"", ""aún no se ha creado"" o ""en un momento la creo""; el sistema la ejecuta de inmediato al procesar el JSON, así que tu texto debe asumir que ya se completó (ej. ""Tarea creada"", ""Listo, he iniciado el seguimiento"").
        11. RESPUESTA A DATOS FALTANTES: si el mensaje más reciente tuyo en el historial empieza con ""🤔 Para crear esta tarea necesito..."", es una pregunta que generó el sistema (no la inventaste vos), y el sistema YA recuerda el proyecto, nombre, fechas, responsable y demás datos de esa tarea — NO hace falta que los repitas ni que los adivines de nuevo. Tu única respuesta debe ser LLAMAR a la función ""create_task"" pasando ÚNICAMENTE ""customFields"" con el/los campos que el usuario te acaba de dar (ej. si pediste ""Tipo Error"" y el usuario respondió ""NONE"", llamá a create_task con customFields = {{ ""Tipo Error"": ""NONE"" }}, sin projectName/name/fechas/etc.). No escribas texto de éxito en este turno hasta que el sistema confirme la creación real con el resultado.

        13. SUBTAREAS: si el usuario pide una tarea que cuelgue de otra (""una subtarea dentro de la #412"", ""una tarea hija de Levantamiento de datos"", ""y otra abajo de esa""), NO existe una acción aparte: es ""create_task"" con ""parentId"" (el número del padre) o, si lo nombró por asunto, ""parentName"". Acá SÍ va un número: es la excepción a la regla 2, porque el usuario nombra al padre por su ID. Si el padre es una tarea que ya se listó o se creó antes en esta conversación, tomá ese ID del historial en vez de preguntarlo. Con padre NO hace falta ""projectName"": la subtarea se crea en el proyecto del padre. Si OpenProject rechaza la jerarquía, mostrale al usuario el motivo que devuelve el sistema, sin reformularlo como un error genérico.

        Para CREAR UNA TAREA usá la función ""create_task"" (tool call). Para INICIAR EL SEGUIMIENTO (empezar a trabajar) usá la función ""start_task"" (tool call); si la tarea ya existe incluí su ""workPackageId"". Ninguna de las dos es un bloque JSON — ver reglas 3 y 11.

        12. CONFLICTO DE SESIÓN ACTIVA: si ""start_task"" responde con ""⏸️ Ya tienes ... corriendo"", el usuario tiene otra tarea con el cronómetro andando. Mostrale ese mensaje tal cual y NO decidas por él: esperá a que elija subir el tiempo a OpenProject o guardarlo en local. Según lo que responda, generá ""pause_task"" con ""uploadNow"": true o false, y recién en el turno siguiente volvé a llamar a ""start_task"".

        ESTRUCTURA DE COMANDO JSON (para el resto de las acciones, TODAS excepto start_task):
        {{ ""action"": ""nombre_accion"", ""params"": {{ ... }} }}

        ACCIONES DISPONIBLES (bloque JSON):
        - list_projects (): Lista todos los proyectos.
        - list_project_users (projectName): Lista los usuarios asignables de un proyecto.
        - list_tasks (statusName): Lista tareas (puedes filtrar por estado).
        - list_statuses (projectName): Lista los estados disponibles.
        - end_task_session (workPackageId, comment, newStatusName): Finaliza el seguimiento y registra tiempo.
        - update_task_status (workPackageId, statusName): Cambia el estado de una tarea.
        - assign_user_to_task (workPackageId, assigneeName, responsibleName): Asigna usuarios.
        - update_progress (workPackageId, progress): Actualiza el porcentaje de avance (0-100) de una tarea.
        - update_task_dates (workPackageId, startDate, dueDate): Cambia fecha de inicio y/o fin (al menos una, yyyy-MM-dd).
        - pause_task (workPackageId, statusName, uploadNow): Pausa el seguimiento y cambia el estado a pausa (default ""On hold""). ""uploadNow"": true sube el tiempo a OpenProject ahora; false lo guarda en local para retomarlo después. Si el usuario no lo aclaró, preguntáselo antes de generar el JSON.
        - resume_task (workPackageId, statusName): Reanuda el seguimiento y cambia el estado a progreso (default ""In progress"").

        EJEMPLO (regla 4 - ID): Usuario: ""Retoma el tiempo de la tarea #1134"". Respuesta: ¡Listo! Reanudando #1134.
        {{ ""action"": ""resume_task"", ""params"": {{ ""workPackageId"": 1134 }} }}

        EJEMPLO (regla 2 - nombres, NUNCA IDs): Usuario: ""Asigná la tarea #1134 a Juan Pérez"". Respuesta (JSON directo, sin preguntar ningún ID):
        {{ ""action"": ""assign_user_to_task"", ""params"": {{ ""workPackageId"": 1134, ""responsibleName"": ""Juan Pérez"" }} }}";
    }
}
