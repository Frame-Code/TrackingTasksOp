namespace Application.Ports.UseCases.Tasks;

/// <summary>
/// Sube bajo demanda las sesiones de una tarea que quedaron guardadas solo en local.
/// Existe para recuperar el tiempo ya acumulado sin obligar a finalizar la tarea.
/// </summary>
public interface IUploadPendingSessionsCommand
{
    /// <summary>Devuelve cuántas sesiones se registraron en OpenProject.</summary>
    Task<int> Execute(int workPackageId, CancellationToken ct = default);
}
