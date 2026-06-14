using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Services.Bot;

public class BotActionExecutor : IBotActionExecutor
{
    private readonly Dictionary<string, IBotActionHandler> _handlers;
    private readonly ILogger<BotActionExecutor> _logger;

    public BotActionExecutor(IEnumerable<IBotActionHandler> handlers, ILogger<BotActionExecutor> logger)
    {
        _handlers = handlers.ToDictionary(h => h.ActionName, h => h, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<List<string>> ExecuteAllAsync(IEnumerable<string> jsonBlocks, CancellationToken ct = default)
    {
        var resultMessages = new List<string>();
        int? lastCreatedWpId = null;

        foreach (var block in jsonBlocks)
        {
            try
            {
                var actionData = JsonSerializer.Deserialize<GroqAction>(block, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (actionData == null) continue;

                var msg = await ExecuteAction(actionData, lastCreatedWpId, ct);

                if (actionData.Action == "start_task" && msg.Contains("ID:"))
                {
                    var match = Regex.Match(msg, @"ID:\s*(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int newId))
                        lastCreatedWpId = newId;
                }
                resultMessages.Add(msg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing action block: {Block}", block);
            }
        }

        return resultMessages;
    }

    private async Task<string> ExecuteAction(GroqAction action, int? contextWpId, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(action.Action, out var handler))
            return $"⚠️ Acción '{action.Action}' no reconocida.";

        try
        {
            return await handler.ExecuteAsync(action, contextWpId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar acción {Action}", action.Action);
            return $"❌ Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Extrae los bloques JSON con clave "action" de un texto de respuesta del LLM.
    /// </summary>
    public static List<string> ExtractJsonBlocks(string text)
    {
        var blocks = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            int start = text.IndexOf('{', i);
            if (start == -1) break;
            int depth = 0, end = -1;
            for (int j = start; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}') depth--;

                if (depth == 0) { end = j; break; }
            }

            if (end != -1)
            {
                string block = text[start..(end + 1)];
                if (block.Contains("\"action\"")) blocks.Add(block);
                i = end + 1;
            }
            else break;
        }
        return blocks;
    }
}
