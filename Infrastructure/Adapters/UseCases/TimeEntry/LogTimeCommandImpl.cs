using System.ComponentModel.DataAnnotations;
using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Domain.Entities.TrackingTasksEntities;

namespace Infrastructure.Adapters.UseCases.TimeEntry;

public class LogTimeCommandImpl(
    ITaskRepository repository,
    IStartTaskCommand startTaskCommand,
    IAddTimeEntryCommand addTimeEntryCommand,
    IActivityOpService activityOpService,
    CurrentUser currentUser) : ILogTimeCommand
{
    /// <summary>Tope defensivo: más de 24 h en un mismo día siempre es un error de tipeo.</summary>
    private const double MaxHoursPerEntry = 24;

    public async Task<double> Execute(LogTimeRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var hours = ResolveHours(request);
        ValidateSpentOn(request.SpentOn);

        // La tarea debe existir en local para poder guardar el detalle. StartTracking = false
        // solo la registra: no abre ninguna sesión de cronómetro.
        var task = await repository.GetByIdAsync(request.WorkPackageId)
                   ?? await RegisterTaskLocally(request);

        var activityId = request.ActivityId is > 0
            ? request.ActivityId.Value
            : await ResolveDefaultActivityId(request.WorkPackageId);

        // Primero OpenProject: si falla, no queda un detalle local marcado como subido
        // que después nadie volvería a intentar registrar.
        await addTimeEntryCommand.Execute(new AddTimeEntryRequest(
            request.WorkPackageId, activityId, hours, request.Comment, request.SpentOn));

        var start = request.SpentOn.ToDateTime(request.StartTime ?? new TimeOnly(0, 0));
        var details = task.TasksTimeDetails.ToList();
        details.Add(new TaskTimeDetail
        {
            StartTime = start,
            EndTime = start.AddHours(hours),
            Uploaded = true,
            UserId = userId,
            IdTask = task.WorkPackageId
        });
        task.TasksTimeDetails = details;

        await repository.SaveAsync(task);
        return hours;
    }

    /// <summary>
    /// Las horas mandan; si no se indicaron, se derivan del rango horario.
    /// Se expone para poder probar los casos límite sin tocar OpenProject.
    /// </summary>
    internal static double ResolveHours(LogTimeRequest request)
    {
        var hours = request.Hours;

        if (hours is null && request is { StartTime: not null, EndTime: not null })
        {
            // Se comparan los extremos antes de restar: TimeOnly - TimeOnly nunca da negativo
            // (asume que el rango cruza medianoche), así que 17:00→09:00 devolvería 16 h
            // en vez de delatar que el usuario invirtió las horas.
            if (request.EndTime.Value <= request.StartTime.Value)
                throw new ValidationException("La hora de finalización debe ser posterior a la de inicio.");

            hours = (request.EndTime.Value - request.StartTime.Value).TotalHours;
        }

        if (hours is null)
            throw new ValidationException("Indica las horas trabajadas, o la hora de inicio y de finalización.");

        if (hours is <= 0)
            throw new ValidationException("Las horas deben ser mayores que cero.");

        if (hours > MaxHoursPerEntry)
            throw new ValidationException($"No se pueden registrar más de {MaxHoursPerEntry} horas en una sola entrada.");

        return Math.Round(hours.Value, 2);
    }

    private static void ValidateSpentOn(DateOnly spentOn)
    {
        if (spentOn > DateOnly.FromDateTime(DateTime.Now))
            throw new ValidationException("No se puede registrar tiempo en una fecha futura.");
    }

    private async Task<Domain.Entities.TrackingTasksEntities.Task> RegisterTaskLocally(LogTimeRequest request)
    {
        if (request.ProjectId is not > 0 || request.StatusId is not > 0 || string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException(
                $"La tarea #{request.WorkPackageId} no está registrada localmente y faltan datos para registrarla.");

        return await startTaskCommand.Execute(new StarTaskRequest
        {
            WorkPackageId = request.WorkPackageId,
            ProjectId = request.ProjectId.Value,
            StatusId = request.StatusId.Value,
            Name = request.Name,
            StartTracking = false
        });
    }

    private async Task<int> ResolveDefaultActivityId(int workPackageId)
    {
        var activities = await activityOpService.Lists(workPackageId);
        var defaultActivity = activities.FirstOrDefault()
            ?? throw new Exception($"No se encontró ninguna actividad disponible para registrar el tiempo de la tarea #{workPackageId}.");

        return defaultActivity.Id;
    }
}
