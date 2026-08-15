using Application.Dto.TimeEntry;

namespace Application.Ports.UseCases.TimeEntry;

/// <summary>
/// Registra tiempo a mano en OpenProject y lo deja reflejado en el historial local,
/// sin necesidad de haber corrido el cronómetro.
/// </summary>
public interface ILogTimeCommand
{
    /// <summary>Devuelve las horas finalmente registradas.</summary>
    Task<double> Execute(LogTimeRequest request, CancellationToken ct = default);
}
