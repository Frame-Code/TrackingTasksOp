namespace Infrastructure.Exceptions;

/// <summary>
/// Se lanza cuando el usuario intenta abrir una sesión de tiempo teniendo otra tarea
/// ya corriendo. Invariante del sistema: un usuario tiene como máximo UNA sesión abierta.
/// Quien la captura debe ofrecerle al usuario pausar la tarea en curso (subiendo el tiempo
/// a OpenProject o guardándolo en local) antes de reintentar.
/// </summary>
public class ActiveSessionConflictException(int workPackageId, string taskName, DateTime startedAt)
    : Exception($"La tarea #{workPackageId} ({taskName}) ya tiene una sesión activa desde {startedAt:HH:mm}.")
{
    public int WorkPackageId { get; } = workPackageId;
    public string TaskName { get; } = taskName;
    public DateTime StartedAt { get; } = startedAt;
}
