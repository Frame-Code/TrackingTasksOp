namespace Application.Dto.Tasks;

public record PendingSessionTaskRow(int WorkPackageId, string TaskName, string ProjectName, double Hours);
