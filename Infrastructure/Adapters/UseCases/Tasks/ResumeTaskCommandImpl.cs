using Application.Dto.Tasks;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class ResumeTaskCommandImpl(
    ITaskRepository repository,
    IProjectRepository projectRepository,
    IUpdateWorkPackageCommand updateWorkPackageCommand,
    IGetWorkPackageCommand getWorkPackageCommand,
    IProjectOpService projectOpService,
    CurrentUser currentUser) : IResumeTaskCommand
{
    public async Task<TaskEntity> Execute(ResumeTaskRequest request)
    {
        var userId = currentUser.UserId
            ?? throw new Exception("No se pudo determinar el usuario actual.");

        var task = await repository.GetByIdAsync(request.WorkPackageId);

        if (task is null)
        {
            task = await CreateFromOpenProjectAsync(request, userId);
        }
        else
        {
            var lastDetail = task.TasksTimeDetails.OrderBy(x => x.StartTime).LastOrDefault();
            if (lastDetail is not null && lastDetail.EndTime is null)
                throw new InvalidOperationException($"Task with OpenProjectId {request.WorkPackageId} already has an active session.");

            if (request.InProgressStatusId.HasValue && request.InProgressStatusId.Value > 0)
            {
                await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.InProgressStatusId.Value);
                task.StatusTaskId = request.InProgressStatusId.Value;
            }
        }

        var details = task.TasksTimeDetails.ToList();
        details.Add(new TaskTimeDetail { UserId = userId, IdTask = task.WorkPackageId });
        task.TasksTimeDetails = details;

        return await repository.SaveAsync(task);
    }

    /// <summary>
    /// La tarea nunca fue iniciada localmente (no hay historial de sesiones), por lo que
    /// "reanudar" equivale a comenzar el seguimiento por primera vez: traemos los datos
    /// del work package desde OpenProject y creamos el registro local, sincronizando el
    /// proyecto si tampoco existe localmente (mismo patrón que StartTaskCommandImpl).
    /// </summary>
    private async Task<TaskEntity> CreateFromOpenProjectAsync(ResumeTaskRequest request, string userId)
    {
        var openProjectInstanceId = currentUser.OpenProjectInstanceId
            ?? throw new Exception("El usuario actual no tiene una instancia de OpenProject configurada.");

        var workPackage = await getWorkPackageCommand.Execute(request.WorkPackageId)
            ?? throw new ArgumentException($"Task with OpenProjectId {request.WorkPackageId} does not exist");

        int projectId = ExtractId(workPackage.Links.Project.Href);
        if (projectId <= 0)
            throw new Exception($"No se pudo determinar el proyecto del work package #{request.WorkPackageId}.");

        if (await projectRepository.GetByIdAsync(projectId) is null)
        {
            var opProject = (await projectOpService.Lists()).FirstOrDefault(p => p.Id == projectId);
            if (opProject != null)
            {
                await projectRepository.SaveAsync(new Project
                {
                    Id = opProject.Id,
                    Name = opProject.Name,
                    Identifier = opProject.Identifier,
                    IsActive = opProject.IsActive,
                    OpenProjectInstanceId = openProjectInstanceId
                });
            }
        }

        int statusId = request.InProgressStatusId is > 0
            ? request.InProgressStatusId.Value
            : ExtractId(workPackage.Links.Status.Href);

        if (request.InProgressStatusId.HasValue && request.InProgressStatusId.Value > 0)
        {
            await updateWorkPackageCommand.Execute(request.WorkPackageId, statusId: request.InProgressStatusId.Value);
        }

        return new TaskEntity
        {
            WorkPackageId = workPackage.Id,
            UserId = userId,
            OpenProjectInstanceId = openProjectInstanceId,
            Name = workPackage.Subject,
            ProjectId = projectId,
            StatusTaskId = statusId
        };
    }

    private static int ExtractId(string href) =>
        int.TryParse(href.Split('/').LastOrDefault(), out var id) ? id : 0;
}
