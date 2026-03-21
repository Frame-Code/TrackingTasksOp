using Application.Ports.UseCases.Tasks;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskCommandImpl : IEndTaskCommand
{
    public Task<Task> Execute(Task request)
    {
        throw new NotImplementedException();
    }
}