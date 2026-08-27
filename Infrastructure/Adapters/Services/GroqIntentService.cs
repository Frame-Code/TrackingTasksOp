using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Application.Dto.Conversation;
using Application.Ports.Services;
using Infrastructure.Adapters.Services.Bot;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Services
{
    /// <summary>
    /// Orquestador del bot conversacional: aplica heurísticas rápidas, consulta a Groq
    /// cuando es necesario y delega la ejecución de acciones a <see cref="IBotActionExecutor"/>.
    /// </summary>
    public class GroqIntentService(
        ILogger<GroqIntentService> logger,
        IConversationContextService conversationContextService,
        IBotIntentInterceptor intentInterceptor,
        IGroqApiClient groqApiClient,
        IBotActionExecutor botActionExecutor) : IAiIntentService
    {
        public async Task<string> GetIntentAsync(string prompt, string sessionId, CancellationToken ct = default)
        {
            var context = await conversationContextService.GetOrCreateAsync(sessionId, ct);

            string normalizedPrompt = intentInterceptor.Normalize(prompt);
            logger.LogInformation("Processing normalized prompt: {Prompt}", normalizedPrompt);

            // 1. Capa de interceptación heurística (ahorro de LLM / respuesta rápida)
            var quickReply = await intentInterceptor.TryInterceptAsync(normalizedPrompt, ct);
            if (quickReply != null) return await SaveContext(context, prompt, quickReply, ct);

            // 2. Capa LLM — Groq
            if (!groqApiClient.IsConfigured)
                return await SaveContext(context, prompt, "⚠️ API Key de Groq no configurada.", ct);

            GroqCompletionResult completion;
            try
            {
                completion = await groqApiClient.GetCompletionAsync(context, prompt, ct);
            }
            catch (GroqApiException ex)
            {
                // El cuerpo crudo trae JSON, nombre del modelo e ID de organización: va al log,
                // que es donde sirve, y nunca a la burbuja del chat.
                logger.LogError(ex, "Groq API falló. Kind={Kind} Status={Status} Body={Body}",
                    ex.Kind, ex.StatusCode, ex.Body);
                return await SaveContext(context, prompt, UserFacingMessage(ex.Kind), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calling Groq API");
                return await SaveContext(context, prompt,
                    "😕 No pude comunicarme con el asistente. Revisá tu conexión e intentá de nuevo.", ct);
            }

            // Tool calls nativas (ej. start_task) tienen prioridad: son estructuradas, no requieren
            // parsear texto. El resto de las acciones siguen viniendo como JSON embebido en el texto.
            var jsonBlocks = completion.ToolCalls.Count > 0
                ? completion.ToolCalls.Select(BuildJsonBlockFromToolCall).ToList()
                : (completion.Text.Contains("\"action\"") ? BotActionExecutor.ExtractJsonBlocks(completion.Text) : []);

            if (jsonBlocks.Count > 0)
            {
                var resultMessages = await botActionExecutor.ExecuteAllAsync(jsonBlocks, context, ct);

                if (resultMessages.Count > 0)
                {
                    // Con tool calls el texto no trae el JSON embebido, así que no hace falta limpiarlo.
                    string textPart = completion.ToolCalls.Count > 0
                        ? completion.Text.Trim()
                        : Regex.Replace(completion.Text, @"\{.*\}", "", RegexOptions.Singleline).Trim();
                    string finalResponse = string.Join("\n", resultMessages);
                    if (!string.IsNullOrWhiteSpace(textPart) && !textPart.StartsWith("{"))
                        finalResponse = $"{textPart}\n\n{finalResponse}";

                    return await SaveContext(context, prompt, finalResponse, ct,
                        SummarizeForModel(finalResponse, textPart, jsonBlocks));
                }
            }

            return await SaveContext(context, prompt, completion.Text, ct);
        }

        /// <summary>
        /// A partir de acá, un resultado se considera "grande" y al modelo se le manda un
        /// resumen en vez del texto completo. Por debajo del umbral viaja tal cual.
        ///
        /// El número separa dos poblaciones bien distintas: los mensajes de control rondan los
        /// 100-300 caracteres y una lista de tareas arranca en más de mil. No hay nada en el
        /// medio, así que el valor exacto no es delicado.
        /// </summary>
        private const int MaxHistoryCharsForModel = 600;

        /// <summary>
        /// Qué se le reenvía al modelo en los turnos siguientes, en lugar del resultado completo.
        ///
        /// Solo se resume lo grande, y eso NO es una optimización: los mensajes de control cortos
        /// ("⏸️ Ya tienes ... corriendo", "🤔 Para crear esta tarea necesito...") tienen que
        /// llegarle textuales, porque las reglas 11 y 12 del system prompt le piden reconocerlos
        /// literalmente para continuar el flujo. Resumirlos rompería crear-tarea-con-campos-
        /// faltantes y el conflicto de sesión activa.
        ///
        /// Devuelve null cuando no hace falta resumir: ahí el historial guarda un solo texto.
        /// </summary>
        private static string? SummarizeForModel(string fullResponse, string textPart, List<string> jsonBlocks)
        {
            if (fullResponse.Length <= MaxHistoryCharsForModel) return null;

            var actions = string.Join(", ", jsonBlocks.Select(ActionNameOf)
                                                      .Where(name => !string.IsNullOrEmpty(name))
                                                      .Distinct());
            var summary = string.IsNullOrEmpty(actions)
                ? "[Resultado mostrado al usuario en pantalla; no lo repitas.]"
                : $"[Ejecuté {actions}. El resultado completo ya se le mostró al usuario en pantalla; no lo repitas.]";

            // El texto propio del modelo sí se conserva: es corto y es lo que mantiene el hilo
            // de la conversación.
            return string.IsNullOrWhiteSpace(textPart) || textPart.StartsWith('{')
                ? summary
                : $"{textPart}\n{summary}";
        }

        private static string? ActionNameOf(string jsonBlock)
        {
            try
            {
                return JsonNode.Parse(jsonBlock)?["action"]?.GetValue<string>();
            }
            catch (JsonException)
            {
                // Un bloque ilegible ya lo loguea BotActionExecutor al intentar ejecutarlo;
                // acá solo significa que no aporta nombre al resumen.
                return null;
            }
        }

        /// <summary>
        /// Lo único que ve el usuario final: sin JSON, sin status codes y con algo que pueda
        /// hacer al respecto. El detalle técnico ya quedó en el log.
        /// </summary>
        private static string UserFacingMessage(GroqFailureKind kind) => kind switch
        {
            GroqFailureKind.RateLimited =>
                "⏳ El asistente está recibiendo muchas consultas seguidas. Esperá unos segundos y volvé a preguntar.",
            GroqFailureKind.Authentication =>
                "🔑 La clave del asistente no es válida o expiró. Revisala en Configuración → Asistente IA.",
            _ =>
                "😕 No pude procesar tu pedido. Probá reformulándolo con otras palabras."
        };

        private static string BuildJsonBlockFromToolCall(GroqToolCall call)
        {
            var wrapper = new JsonObject
            {
                ["action"] = call.Name,
                ["params"] = JsonNode.Parse(call.ArgumentsJson) ?? new JsonObject()
            };
            return wrapper.ToJsonString();
        }

        private async Task<string> SaveContext(ConversationContext context, string prompt, string response,
            CancellationToken ct, string? modelContent = null)
        {
            context.AddUserMessage(prompt);
            context.AddTtmMessage(response, modelContent);
            await conversationContextService.SaveAsync(context, ct);
            return response;
        }
    }
}
