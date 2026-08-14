namespace Application.Dto.TimeEntry;

/// <summary>
/// <paramref name="SpentOn"/> es la fecha real en que se trabajó. Si es null se usa hoy.
/// Importante para sesiones que se cierran en un día distinto al que empezaron: el reporte
/// se construye a partir de este campo en OpenProject.
/// </summary>
public record AddTimeEntryRequest(
    int IdWorkPackage,
    int IdActivity,
    double Hours,
    string Comment,
    DateOnly? SpentOn = null
    );