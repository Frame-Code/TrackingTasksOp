using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TaskController(IStartTaskCommand command) : ControllerBase
{
    [HttpPost]
    public async Task<TaskEntity> StartTask([FromBody] StarTaskRequest request)
    {
        return await command.Execute(request);
    }
}