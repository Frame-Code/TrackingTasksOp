using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Exceptions;

namespace Infrastructure.Adapters.Services.Bot.Actions;

/// <summary>
/// Inicia el seguimiento de tiempo. A diferencia de "create_task", esta acción SÍ arranca
/// el cronómetro, y falla con un mensaje de opciones si ya hay otra tarea corriendo.
/// </summary>
public class StartTaskActionHandler(
    IStartTaskCommand startTaskCommand,
    IStatusOpService statusOpService,
    IOpenProjectEntityResolver entityResolver,
    ICreateWorkPackageCommand createWorkPackageCommand) : IBotActionHandler
{
    public string ActionName => "start_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var built = await StartTaskRequestBuilder.BuildAsync(
            action, contextWpId, conversationContext, statusOpService, entityResolver, createWorkPackageCommand);

        if (built.PendingMessage is not null) return built.PendingMessage;

        var startReq = CloneWithTracking(built.Request!);

        try
        {
            var task = await startTaskCommand.Execute(startReq);
            return $"▶️ Seguimiento iniciado para **{task.Name}** (ID: {task.WorkPackageId}).";
        }
        catch (ActiveSessionConflictException ex)
        {
            var elapsed = DateTime.Now - ex.StartedAt;
            return $"⏸️ Ya tienes **{ex.TaskName}** (#{ex.WorkPackageId}) corriendo desde hace " +
                   $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m.\n\n" +
                   "Para iniciar esta tarea primero hay que cerrar esa sesión. ¿Qué prefieres?\n" +
                   $"- **Subirla ahora** a OpenProject → dime \"pausa la #{ex.WorkPackageId} y súbela\"\n" +
                   $"- **Guardarla en local** para retomarla después → dime \"pausa la #{ex.WorkPackageId} sin subir\"";
        }
    }

    // StarTaskRequest es una class (no record), así que se clona a mano para forzar StartTracking.
    private static StarTaskRequest CloneWithTracking(StarTaskRequest r) => new()
    {
        WorkPackageId = r.WorkPackageId,
        StartTracking = true,
        ActivityId = r.ActivityId,
        Comment = r.Comment,
        Name = r.Name,
        Description = r.Description,
        ProjectId = r.ProjectId,
        StatusId = r.StatusId,
        AssigneeId = r.AssigneeId,
        ResponsibleId = r.ResponsibleId,
        StartDate = r.StartDate,
        DueDate = r.DueDate,
        CustomFieldOptionIds = r.CustomFieldOptionIds,
        CustomFieldTextValues = r.CustomFieldTextValues
    };
}
