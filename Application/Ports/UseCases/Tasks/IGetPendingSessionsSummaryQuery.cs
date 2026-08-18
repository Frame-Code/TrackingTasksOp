using Application.Dto.Tasks;

namespace Application.Ports.UseCases.Tasks;

/// <summary>
/// Resume las sesiones cerradas del usuario que quedaron guardadas solo en local
/// (Uploaded = false), para poder avisarle que tiene tiempo sin registrar en OpenProject.
/// </summary>
public interface IGetPendingSessionsSummaryQuery
{
    Task<PendingSessionsSummaryResponse> Execute(CancellationToken ct = default);
}
