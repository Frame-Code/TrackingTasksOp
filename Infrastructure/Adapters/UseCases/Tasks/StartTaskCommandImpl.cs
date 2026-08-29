using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Dto.WorkPackages;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services;
using Infrastructure.Exceptions;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Tasks;


    public class StartTaskCommandImpl(
        ITaskRepository repository,
           IProjectRepository projectRepository,
          ICreateWorkPackageCommand createWorkPackageCommand,
          IProjectOpService projectOpService,
          CurrentUser currentUser
       ) : IStartTaskCommand
{
   public async Task<TaskEntity> Execute(StarTaskRequest request)
    {
        var userId = currentUser.UserId
            ?? throw new Exception("No se pudo determinar el usuario actual.");
        var openProjectInstanceId = currentUser.OpenProjectInstanceId
            ?? throw new Exception("El usuario actual no tiene una instancia de OpenProject configurada.");

        // 1. Buscar el proyecto por su nombre/identificador
        var project = await projectRepository.GetByIdForInstanceAsync(request.ProjectId, openProjectInstanceId);
       
       // Si no está localmente, intentamos buscarlo en OpenProject y sincronizarlo
        if (project is null)
        {
            var opProjects = await projectOpService.Lists();
            var opProject = opProjects.FirstOrDefault(p => p.Id == request.ProjectId);
            
            if (opProject != null)
            {
                project = new Project
                {
                    Id = opProject.Id,
                    Name = opProject.Name,
                    Identifier = opProject.Identifier,
                    IsActive = opProject.IsActive,
                    OpenProjectInstanceId = openProjectInstanceId
                };
                await projectRepository.SaveAsync(project);
            }
            else
            {
                throw new Exception($"El proyecto con el identificador '{request.ProjectId}' no fue encontrado en la base de datos local ni en OpenProject.");
            }
        }

        // Si el WorkPackageId es 0, significa que debemos crearlo en OpenProject primero
        int workPackageId = request.WorkPackageId;
        TaskEntity? task = null;

        if (workPackageId > 0)
        {
            task = await repository.GetByIdForUserAsync(workPackageId, userId);
        }

        if (task is null)
        {
            // Si no existe localmente, intentamos crearlo en OpenProject si es necesario,
            // o simplemente lo registramos localmente si ya tenemos un ID válido pero no estaba en nuestra DB.
            if (workPackageId <= 0)
            {
                var createRequest = new CreateWorkPackageRequest(
                    request.Name,
                    request.ProjectId,
                    request.StatusId,
                    request.TypeId,
                    null, // PriorityId opcional
                    request.Description,
                    request.AssigneeId,
                    request.ResponsibleId,
                    request.StartDate,
                    request.DueDate,
                    request.CustomFieldOptionIds,
                    request.CustomFieldTextValues,
                    request.EstimatedHours
                );
                var opWorkPackage = await createWorkPackageCommand.Execute(createRequest);
                workPackageId = opWorkPackage.Id;
            }

            task = new TaskEntity
            {
                WorkPackageId = workPackageId,
                UserId = userId,
                OpenProjectInstanceId = openProjectInstanceId,
                Name = request.Name,
                Description = request.Description,
                ProjectId = request.ProjectId,
                StatusTaskId = request.StatusId,
            };
        }

        // Si no se pidió seguimiento, la tarea solo se crea/registra: sin sesión de tiempo.
        if (!request.StartTracking)
            return await repository.SaveAsync(task);

        // Invariante: un usuario tiene como máximo UNA sesión abierta. Si hay otra tarea
        // corriendo, no la cerramos silenciosamente (así se perdía y se corrompía el tiempo):
        // avisamos para que el usuario decida qué hacer con ella.
        var running = await repository.GetActiveByUserAsync(userId);
        if (running is not null && running.WorkPackageId != task.WorkPackageId)
        {
            var openDetail = running.TasksTimeDetails.First(d => d.EndTime == null);
            throw new ActiveSessionConflictException(running.WorkPackageId, running.Name, openDetail.StartTime);
        }

        // Cerrar la última entrada si quedó abierta en esta misma tarea.
        //
        // Esta sesión NO la cerró el usuario: la encontramos abierta al arrancar otra. No
        // sabemos cuándo dejó de trabajar, solo hasta cuándo hubo evidencia de actividad, así
        // que la cerramos con el último latido y la marcamos como inferida.
        //
        // Antes se cerraba con DateTime.Now y, si venía ActivityId, se subía sola a OpenProject
        // (con hasta 40 minutos aleatorios de holgura encima). Una sesión que quedó abierta
        // anoche se publicaba hoy como una jornada entera de trabajo que nunca ocurrió. Un
        // tiempo estimado no se publica: va a pendientes para que el usuario lo confirme.
        var details = task.TasksTimeDetails.ToList();
        var lastDetail = task.GetActiveSession();
        lastDetail?.CloseAsUnconfirmed();

        //Crear la nueva entrada de tiempo
        var detail = new TaskTimeDetail
        {
            UserId = userId,
            IdTask = task.WorkPackageId
        };

        details.Add(detail);
        task.TasksTimeDetails = details;
        return await repository.SaveAsync(task);
    }
}
