namespace Application.Dto.WorkPackages;

/// <summary>
/// Envía null para limpiar una fecha; omite la propiedad para no tocarla.
/// Usa string vacío ("") para indicar borrado desde el frontend.
/// </summary>
public record UpdateWorkPackageDatesRequest(string? StartDate, string? DueDate);
