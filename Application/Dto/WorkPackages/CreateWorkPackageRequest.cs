namespace Application.Dto.WorkPackages;

public record CreateWorkPackageRequest(
    string Subject,
    int ProjectId,
    int? StatusId = null,
    int? TypeId = null,
    int? PriorityId = null,
    string? Description = null,
    int? AssigneeId = null,
    int? ResponsibleId = null,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    Dictionary<string, int>? CustomFieldOptionIds = null,
    Dictionary<string, string>? CustomFieldTextValues = null,

    /// <summary>
    /// Horas estimadas de trabajo ("Trabajo" en OpenProject). Opcional: si es null
    /// el campo no se envía y la tarea queda sin estimación, como hasta ahora.
    /// </summary>
    double? EstimatedHours = null,

    /// <summary>
    /// Work package padre. Si viene, la tarea se crea como subtarea (_links.parent).
    /// El proyecto lo hereda del padre: una subtarea vive donde vive su padre.
    /// </summary>
    int? ParentId = null
);
