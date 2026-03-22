using Application.Dto.Tasks;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Domain.Entities.TrackingTasksEntities;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;

public class StartTaskCommandImpl(ITaskRepository repository) : IStartTaskCommand
{
    public async Task<TaskEntity> Execute(StarTaskRequest request)
    {
        var task = await repository.GetByIdAsync(request.OpenProjectId) 
            ?? new TaskEntity
            {
                OpenProjectId = request.OpenProjectId,
                Name = request.Name,
                Description = request.Description,
                ProjectId = request.ProjectId,
                StatusTaskId = request.StatusId
            };

        var details = task.TasksTimeDetails.ToList();
        var lastDetail = details
            .OrderBy(x => x.StartTime)
            .LastOrDefault();

        if (lastDetail is not null)
            lastDetail.EndTime = DateTime.Now;
        
        var detail = new TaskTimeDetail
        {
            IdTask = request.OpenProjectId
        };
        
        details.Add(detail);
        task.TasksTimeDetails = details;
        return await repository.SaveAsync(task);
    }
}