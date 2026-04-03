namespace Application.Dto.Tasks;

public record EndTaskSessionRequest(
    int WorkPackageId,
    int ActivityId,
    string Comment
    );
