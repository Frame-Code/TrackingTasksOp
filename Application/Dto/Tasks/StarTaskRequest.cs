namespace Application.Dto.Tasks;

public class StarTaskRequest
{
    public int WorkPackageId { get; init; }

    /// <summary>
    /// Si es true, además de crear/registrar la tarea abre una sesión de tiempo.
    /// Por defecto false: crear una tarea NO arranca el cronómetro — eso lo decide
    /// el usuario explícitamente con "Iniciar".
    /// </summary>
    public bool StartTracking { get; init; } = false;
    public int? ActivityId { get; init; }
    public string? Comment { get; init; }
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public int ProjectId { get; init; }
    public int StatusId { get; init; }

    /// <summary>Tipo de work package (DESARROLLO, ERROR, etc.). Si es null, OpenProject resuelve uno por defecto.</summary>
    public int? TypeId { get; init; }

    /// <summary>Padre del que cuelga la tarea al crearla. Null = tarea raíz (lo de siempre).</summary>
    public int? ParentId { get; init; }

    /// <summary>Horas estimadas de trabajo. Opcional: si es null la tarea queda sin estimación.</summary>
    public double? EstimatedHours { get; init; }
    public int? AssigneeId { get; init; }
    public int? ResponsibleId { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public Dictionary<string, int>? CustomFieldOptionIds { get; init; }
    public Dictionary<string, string>? CustomFieldTextValues { get; init; }
}
