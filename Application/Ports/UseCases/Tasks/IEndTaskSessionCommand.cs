using Application.Dto.Tasks;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Application.Ports.UseCases.Tasks;

public interface IEndTaskSessionCommand
{
    Task<Task> Execute(EndTaskSessionRequest request);
}