namespace Application.Dto.WorkPackages;

public record CreateWorkPackageRequest(
    string Subject,
    int ProjectId,
    int? StatusId = null,
    int? TypeId = null,
    int? PriorityId = null,
    string? Description = null,
    int? AssigneeId = null,
    int? ResponsibleId = null
);
