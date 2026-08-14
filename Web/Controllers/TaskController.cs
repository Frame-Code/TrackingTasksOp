using Application.Dto.Tasks;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TaskController(
    IStartTaskCommand startTaskCommand,
    IEndTaskSessionCommand endTaskSessionCommand,
    ICancelTaskSessionCommand cancelTaskSessionCommand,
    IPauseTaskCommand pauseTaskCommand,
    IResumeTaskCommand resumeTaskCommand,
    ITaskRepository taskRepository
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

    [HttpPost("cancel_session")]
    public async Task<IActionResult> CancelSession([FromBody] CancelTaskSessionRequest request)
    {
        var cancelled = await cancelTaskSessionCommand.Execute(request.WorkPackageId);
        return cancelled ? NoContent() : NotFound(new { message = "No hay sesión activa para esta tarea." });
    }

    [HttpPost("pause_session")]
    public async Task<TaskEntity> PauseSession([FromBody] PauseTaskRequest request)
    {
        return await pauseTaskCommand.Execute(request);
    }

    [HttpPost("resume_session")]
    public async Task<TaskEntity> ResumeSession([FromBody] ResumeTaskRequest request)
    {
        return await resumeTaskCommand.Execute(request);
    }

    [HttpGet("{workPackageId:int}")]
    public async Task<ActionResult<TaskEntity>> GetTask(int workPackageId)
    {
        var task = await taskRepository.GetByIdAsync(workPackageId);
        if (task is null) return NotFound();
        return task;
    }
}