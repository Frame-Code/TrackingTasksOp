namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Parsea bloques JSON de acciones devueltos por el LLM y delega su ejecución
/// al <see cref="IBotActionHandler"/> correspondiente.
/// </summary>
public interface IBotActionExecutor
{
    /// <summary>
    /// Deserializa y ejecuta cada bloque JSON de acción, en orden, devolviendo
    /// el mensaje de resultado de cada una.
    /// </summary>
    Task<List<string>> ExecuteAllAsync(IEnumerable<string> jsonBlocks, CancellationToken ct = default);
}
