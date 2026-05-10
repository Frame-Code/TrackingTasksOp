using Application.Dto.TimeEntry;

namespace Application.Ports.UseCases.TimeEntry;

public interface IAddTimeEntryCommand
{
    Task Execute(AddTimeEntryRequest request);
}