using Application.Dto.Tasks;

namespace Application.Ports.UseCases.Tasks;

/// <summary>
/// Lista, una fila por tarea, las sesiones cerradas del usuario que quedaron guardadas solo
/// en local (Uploaded = false), para el modal de "sesiones sin enviar".
/// </summary>
public interface IGetPendingSessionsListQuery
{
    Task<List<PendingSessionTaskRow>> Execute(CancellationToken ct = default);
}
