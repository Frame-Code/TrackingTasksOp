using Application.Dto.Conversation;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

/// <summary>
/// Crea la tarea en OpenProject y la registra localmente SIN arrancar el cronómetro.
/// El seguimiento lo inicia el usuario explícitamente después ("start_task").
/// </summary>
public class CreateTaskActionHandler(
    IStartTaskCommand startTaskCommand,
    IStatusOpService statusOpService,
    IOpenProjectEntityResolver entityResolver,
    ICreateWorkPackageCommand createWorkPackageCommand) : IBotActionHandler
{
    public string ActionName => "create_task";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, ConversationContext? conversationContext = null, CancellationToken ct = default)
    {
        var built = await StartTaskRequestBuilder.BuildAsync(
            action, contextWpId, conversationContext, statusOpService, entityResolver, createWorkPackageCommand);

        if (built.PendingMessage is not null) return built.PendingMessage;

        // StartTracking queda en false (default): crear no arranca el tiempo.
        var task = await startTaskCommand.Execute(built.Request!);

        var parentNote = built.Request!.ParentId is > 0
            ? $" como subtarea de la **#{built.Request.ParentId}**"
            : "";

        var message = $"✅ Tarea **{task.Name}** creada{parentNote} (ID: {task.WorkPackageId}). " +
                      $"El cronómetro **no** está corriendo — dime \"inicia la #{task.WorkPackageId}\" cuando quieras empezar.";

        // Prevención de errores (heurística de Nielsen): si se creó una tarea nueva sin asignar
        // a nadie, avisamos de inmediato, ya que no aparecerá en las vistas "Mis tareas" de OpenProject.
        if (built.Request!.AssigneeId is null && built.Request.ResponsibleId is null)
        {
            message += "\n\n⚠️ No indicaste a quién asignar la tarea, así que quedó sin asignado y no aparecerá en 'Mis tareas' de OpenProject. " +
                       "Si quieres, dime a quién asignarla (puede ser a ti mismo).";
        }

        return message;
    }
}
