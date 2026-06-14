namespace Infrastructure.Exceptions;

/// <summary>
/// Se lanza cuando OpenProject rechaza un cambio de estado (ej. transición no permitida
/// para el rol del usuario), después de que el resto de la operación (registro de tiempo)
/// ya se completó y persistió correctamente.
/// </summary>
public class TaskStatusTransitionException : Exception
{
    public TaskStatusTransitionException(string message) : base(message)
    {
    }
}
