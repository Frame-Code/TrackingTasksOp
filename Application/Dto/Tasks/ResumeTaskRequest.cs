namespace Application.Dto.Tasks;

public record ResumeTaskRequest(int WorkPackageId, int? InProgressStatusId = null);
