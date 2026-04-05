using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Dto.WorkPackages;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using Web.Infrastructure.Adapters.Services;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Web.Infrastructure.Adapters.UseCases.Tasks;


    public class StartTaskCommandImpl(
        ITaskRepository repository,
           IProjectRepository projectRepository, 
          IAddTimeEntry addTimeEntry,
          ICreateWorkPackageCommand createWorkPackageCommand,
          IProjectOpService projectOpService
       ) : IStartTaskCommand
{


   public async Task<TaskEntity> Execute(StarTaskRequest request)
    {

        // 1. Buscar el proyecto por su nombre/identificador
          var project = await projectRepository.GetByIdAsync(request.ProjectId);
       
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
                    IsActive = opProject.IsActive
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
            task = await repository.GetByIdAsync(workPackageId);
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
                    null, // TypeId opcional
                    null, // PriorityId opcional
                    request.Description,
                    request.AssigneeId,
                    request.ResponsibleId
                );
                var opWorkPackage = await createWorkPackageCommand.Execute(createRequest);
                workPackageId = opWorkPackage.Id;
            }

            task = new TaskEntity
            {
                WorkPackageId = workPackageId,
                Name = request.Name,
                Description = request.Description,
                ProjectId = request.ProjectId,
                StatusTaskId = request.StatusId
            };
        }

        //Cerrar la última entrada si es una nueva tarea
        var details = task.TasksTimeDetails.ToList();
        var lastDetail = details
            .OrderBy(x => x.StartTime)
            .LastOrDefault();

<<<<<<< HEAD
        if (lastDetail is not null && lastDetail.EndTime == null)
=======
        if (lastDetail is not null && !lastDetail.EndTime.HasValue)
>>>>>>> 43a2f6a7702ea35cec6982dd843371d7f6bf2b9b
        {
            lastDetail.EndTime = DateTime.Now;
<<<<<<< HEAD

            // Solo intentamos subir a OpenProject si tenemos el ID de actividad
            if (request.ActivityId.HasValue && request.ActivityId.Value > 0)
            {
                var timeEntryRequest = new AddTimeEntryRequest(task.WorkPackageId, request.ActivityId.Value,
                    lastDetail.GetHoursWorked()!.Value.TotalHours, request.Comment ?? string.Empty);

                await addTimeEntry.Execute(timeEntryRequest);
                lastDetail.Uploaded = true;
            }
            else
            {
                // Se queda como guardado localmente pero no subido a OP
                lastDetail.Uploaded = false;
            }
=======
            
            //Agregando más tiempo de holgura ._.
            var time = lastDetail.GetHoursWorked()!.Value.Minutes;
            if (time is >= 10 and <= 60)
                lastDetail.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(10, 20));
            else if (time >= 60)
                lastDetail.EndTime = DateTime.Now.AddMinutes(TimeTrackService.GetRandomMinutes(20, 40));
            
            var timeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, request.ActivityId ?? -1,
                lastDetail.GetHoursWorked()!.Value.TotalHours, request.Comment ?? string.Empty);
            
            await addTimeEntry.Execute(timeEntryRequest);
            lastDetail.Uploaded = true;
>>>>>>> 43a2f6a7702ea35cec6982dd843371d7f6bf2b9b
        }

        //Crear la nueva entrada de tiempo
        var detail = new TaskTimeDetail
        {
            IdTask = task.WorkPackageId
        };

        details.Add(detail);
        task.TasksTimeDetails = details;
        return await repository.SaveAsync(task);
    }
}