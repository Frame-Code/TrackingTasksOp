namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Resultado de una llamada a Groq: el texto plano de la respuesta (si lo hay) y las
/// tool calls estructuradas que haya devuelto la API (si el modelo decidió invocar
/// alguna herramienta declarada en <see cref="GroqTools"/>).
/// </summary>
public class GroqCompletionResult
{
    public string Text { get; set; } = "";
    public List<GroqToolCall> ToolCalls { get; set; } = [];
}

public class GroqToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string ArgumentsJson { get; set; } = "{}";
}
