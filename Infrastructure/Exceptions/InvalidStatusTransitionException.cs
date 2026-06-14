namespace Infrastructure.Exceptions;

/// <summary>
/// Se lanza cuando OpenProject rechaza un cambio de estado porque la transición desde el
/// estado actual al solicitado no está permitida (workflow) para el rol del usuario actual.
/// Incluye los nombres de los estados a los que SÍ se puede transicionar directamente desde
/// el estado actual, para poder sugerirle al usuario un camino alternativo.
/// </summary>
public class InvalidStatusTransitionException : Exception
{
    public List<string> AllowedStatusNames { get; }

    public InvalidStatusTransitionException(string message, List<string> allowedStatusNames) : base(message)
    {
        AllowedStatusNames = allowedStatusNames;
    }

    /// <summary>
    /// Frase adicional sugiriendo a qué estados sí se puede transicionar directamente
    /// desde el estado actual, para incluir en el mensaje mostrado al usuario.
    /// </summary>
    public string BuildSuggestion() =>
        AllowedStatusNames.Count > 0
            ? $" Desde el estado actual puedes cambiar directamente a: {string.Join(", ", AllowedStatusNames)}."
            : "";
}
