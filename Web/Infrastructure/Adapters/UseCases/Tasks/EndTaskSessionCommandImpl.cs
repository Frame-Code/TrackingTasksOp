using Application.Dto.Tasks;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandImpl
    (ITaskRepository repository): IEndTaskSessionCommand
{
    public async Task<Task> Execute(EndTaskSessionRequest request)
    {
        var task = await repository.GetByIdAsync(request.OpenProjectId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.OpenProjectId} does not exist");
        
        if(!task.TasksTimeDetails.Any())
            throw new InvalidOperationException($"Task with OpenProjectId {request.OpenProjectId} haven't any details");

        var lastTimeDetails = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault()
            ?? throw new InvalidOperationException($"Task with OpenProjectId {request.OpenProjectId} haven't any details");;

        lastTimeDetails.EndTime = DateTime.Now;
        return await repository.SaveAsync(task);
    }
}