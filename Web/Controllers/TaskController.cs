using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Auth;
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
    IRecordSessionHeartbeatCommand recordSessionHeartbeatCommand,
    IUploadPendingSessionsCommand uploadPendingSessionsCommand,
    IGetPendingSessionsSummaryQuery getPendingSessionsSummaryQuery,
    IGetPendingSessionsListQuery getPendingSessionsListQuery,
    ILogTimeCommand logTimeCommand,
    ITaskRepository taskRepository,
    CurrentUser currentUser
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

    /// <summary>
    /// Marca que la sesión abierta del usuario sigue viva. El cliente lo llama cada minuto
    /// mientras corre el cronómetro; el servidor sella la hora (no el cliente).
    ///
    /// Es lo que permite cerrar una sesión huérfana con el último momento en que hubo evidencia
    /// de actividad, en vez de asumir que se trabajó hasta que alguien se dio cuenta.
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken ct)
    {
        var alive = await recordSessionHeartbeatCommand.Execute(ct);
        return alive ? NoContent() : NotFound(new { message = "No hay sesión activa." });
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

    /// <summary>
    /// Resumen de sesiones cerradas sin subir a OpenProject, para el recordatorio recurrente.
    /// </summary>
    [HttpGet("pending_summary")]
    public async Task<ActionResult<PendingSessionsSummaryResponse>> GetPendingSummary(CancellationToken ct)
    {
        return await getPendingSessionsSummaryQuery.Execute(ct);
    }

    /// <summary>
    /// Detalle por tarea de las sesiones cerradas sin subir a OpenProject, para el modal
    /// de "sesiones sin enviar".
    /// </summary>
    [HttpGet("pending_sessions")]
    public async Task<ActionResult<List<PendingSessionTaskRow>>> GetPendingSessions(CancellationToken ct)
    {
        return await getPendingSessionsListQuery.Execute(ct);
    }

    [HttpGet("{workPackageId:int}")]
    public async Task<ActionResult<TaskEntity>> GetTask(int workPackageId)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var task = await taskRepository.GetByIdForUserAsync(workPackageId, userId);
        if (task is null) return NotFound();
        return task;
    }
}