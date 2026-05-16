namespace Application.Dto.ListWorkPackages
{
    public record ListsWorkPackagesRequest(
        int? ProjectId = null,
        int offset = 0,
        int pageSize = 50,
        string? AssigneeId = null,
        string? StatusOperator = null,
        int? StatusId = null
    );
}
