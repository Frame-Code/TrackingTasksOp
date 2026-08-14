using Domain.Entities.OpenProjectEntities.TimeEntries;

namespace Application.Ports.Services;

public interface ITimeEntryOpService
{
    /// <summary>
    /// Trae todas las entradas de tiempo registradas en OpenProject entre dos fechas
    /// (inclusive), opcionalmente filtradas por usuario. Recorre toda la paginación.
    /// </summary>
    Task<List<OpTimeEntry>> Lists(DateOnly from, DateOnly to, int? userId = null);
}
