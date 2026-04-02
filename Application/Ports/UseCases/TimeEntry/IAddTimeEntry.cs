using Application.Dto.TimeEntry;

namespace Application.Ports.UseCases.TimeEntry;

public interface IAddTimeEntry
{
    Task Execute(AddTimeEntryRequest request);
}