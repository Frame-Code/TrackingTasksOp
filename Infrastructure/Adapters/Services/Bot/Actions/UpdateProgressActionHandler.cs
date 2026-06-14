using Application.Ports.UseCases.WorkPackages;

namespace Infrastructure.Adapters.Services.Bot.Actions;

public class UpdateProgressActionHandler(
    IUpdateWorkPackageCommand updateWorkPackageCommand) : IBotActionHandler
{
    public string ActionName => "update_progress";

    public async Task<string> ExecuteAsync(GroqAction action, int? contextWpId, CancellationToken ct = default)
    {
        var p = action.Params;
        int wpId = GroqActionParams.GetInt(p, "workPackageId", "id", "wpId");
        if (wpId == 0 && contextWpId.HasValue) wpId = contextWpId.Value;
        if (wpId <= 0) throw new Exception("Se requiere un ID de tarea válido (workPackageId).");

        int? progress = GroqActionParams.GetNullableInt(p, "progress", "percentageDone", "percentage");
        if (!progress.HasValue) throw new Exception("Se requiere el porcentaje de progreso (progress).");
        if (progress.Value < 0 || progress.Value > 100) throw new Exception("El progreso debe estar entre 0 y 100.");

        await updateWorkPackageCommand.Execute(wpId, percentageDone: progress.Value);
        return $"📊 Progreso de la tarea #{wpId} actualizado a {progress.Value}%.";
    }
}
