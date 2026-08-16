using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.UseCases.TimeEntry;
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
    IUploadPendingSessionsCommand uploadPendingSessionsCommand,
    ILogTimeCommand logTimeCommand,
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

    /// <summary>
    /// Registra en OpenProject las sesiones que quedaron guardadas solo en local.
    /// Permite recuperar el tiempo pendiente sin tener que finalizar la tarea.
    /// </summary>
    [HttpPost("upload_pending")]
    public async Task<IActionResult> UploadPending([FromBody] UploadPendingSessionsRequest request, CancellationToken ct)
    {
        var uploaded = await uploadPendingSessionsCommand.Execute(request.WorkPackageId, ct);
        return Ok(new { uploaded });
    }

    /// <summary>
    /// Registra tiempo a mano, para sesiones que no se cronometraron.
    /// Equivale al formulario "Tiempo registrado" de OpenProject.
    /// </summary>
    [HttpPost("log_time")]
    public async Task<IActionResult> LogTime([FromBody] LogTimeRequest request, CancellationToken ct)
    {
        var hours = await logTimeCommand.Execute(request, ct);
        return Ok(new { hours });
    }

    [HttpGet("{workPackageId:int}")]
    public async Task<ActionResult<TaskEntity>> GetTask(int workPackageId)
    {
        var task = await taskRepository.GetByIdAsync(workPackageId);
        if (task is null) return NotFound();
        return task;
    }
}