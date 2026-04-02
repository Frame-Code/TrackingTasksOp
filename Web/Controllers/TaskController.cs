using Application.Dto.Tasks;
using Application.Ports.UseCases.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TaskController(
    IStartTaskCommand startTaskCommand,
    IEndTaskSessionCommand endTaskSessionCommand
    ) : ControllerBase
{
    [HttpPost("start_session")]
    public async Task<TaskEntity> StartTask([FromBody] StarTaskRequest request)
    {
        return await startTaskCommand.Execute(request);
    }
    
    [HttpPost("end_session")]
    public async Task<TaskEntity> EndTaskSession([FromBody] EndTaskSessionRequest request)
    {
        return await endTaskSessionCommand.Execute(request);
    }
}