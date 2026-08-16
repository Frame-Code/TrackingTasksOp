namespace Application.Dto.TimeEntry;

/// <summary>
/// Registro manual de tiempo, para cuando se olvidó usar el cronómetro.
/// Equivale al formulario "Tiempo registrado" de OpenProject.
/// <para>
/// <paramref name="Hours"/> es la fuente de verdad. <paramref name="StartTime"/> y
/// <paramref name="EndTime"/> son opcionales: si vienen y no se indicaron horas, las horas
/// se calculan a partir de ellas.
/// </para>
/// <para>
/// <paramref name="ProjectId"/>, <paramref name="StatusId"/> y <paramref name="Name"/> solo
/// se usan si la tarea todavía no está registrada localmente.
/// </para>
/// </summary>
public record LogTimeRequest(
    int WorkPackageId,
    DateOnly SpentOn,
    double? Hours = null,
    int? ActivityId = null,
    string Comment = "",
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int? ProjectId = null,
    int? StatusId = null,
    string? Name = null
);
