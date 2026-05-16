namespace Application.Ports.Services;

public interface IHeuristicIntentHandler
{
    /// <summary>
    /// Intenta procesar un prompt de usuario.
    /// </summary>
    /// <param name="prompt">El texto ingresado por el usuario.</param>
    /// <returns>La respuesta formateada si pudo procesarse, de lo contrario null.</returns>
    Task<string?> HandleAsync(string prompt);
}
