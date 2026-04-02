namespace Application.Dto.TimeEntry;

public record AddTimeEntryRequest(
    int IdWorkPackage,
    int IdActivity,
    double Hours,
    string Comment
    );