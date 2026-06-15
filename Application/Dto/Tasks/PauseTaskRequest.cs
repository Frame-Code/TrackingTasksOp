namespace Application.Dto.Tasks;

public record PauseTaskRequest(int WorkPackageId, int? OnHoldStatusId = null);
