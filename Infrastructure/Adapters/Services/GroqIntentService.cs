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

                    return await SaveContext(context, prompt, finalResponse, ct);
                }
            }

            return await SaveContext(context, prompt, completion.Text, ct);
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

        private async Task<string> SaveContext(ConversationContext context, string prompt, string response, CancellationToken ct)
        {
            context.AddUserMessage(prompt);
            context.AddTtmMessage(response);
            await conversationContextService.SaveAsync(context, ct);
            return response;
        }
    }
}
