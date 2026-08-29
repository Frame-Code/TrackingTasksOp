using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.Activity;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Exceptions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandTests
{
    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock(bool addRandomSlackTime)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625
        var mock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        mock.Setup(x => x.FindByIdAsync("user-1"))
            .ReturnsAsync(new ApplicationUser { Id = "user-1", AddRandomSlackTime = addRandomSlackTime });
        return mock;
    }

    private static (EndTaskSessionCommandImpl useCase,
                    Mock<ITaskRepository> repositoryMock,
                    Mock<IAddTimeEntryCommand> addTimeEntryMock,
                    Mock<IUpdateWorkPackageCommand> updateMock,
                    Mock<IActivityOpService> activityOpServiceMock)
        BuildUseCase(TaskEntity? taskFromRepo, bool addRandomSlackTime = true)
    {
        var repositoryMock = new Mock<ITaskRepository>();
        repositoryMock
            .Setup(x => x.GetByIdForUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(taskFromRepo);

        repositoryMock
            .Setup(x => x.SaveAsync(It.IsAny<TaskEntity>()))
            .ReturnsAsync((TaskEntity t) => t);

        var addTimeEntryMock = new Mock<IAddTimeEntryCommand>();
        addTimeEntryMock
            .Setup(x => x.Execute(It.IsAny<AddTimeEntryRequest>()))
            .Returns(Task.CompletedTask);

        var updateMock = new Mock<IUpdateWorkPackageCommand>();
        updateMock
            .Setup(x => x.Execute(
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var activityOpServiceMock = new Mock<IActivityOpService>();
        activityOpServiceMock
            .Setup(x => x.Lists(It.IsAny<int>()))
            .ReturnsAsync(new List<ActivityAllowedValue> { new() { Id = 99, Name = "Development" } });

        // PendingTimeUploaderImpl real sobre los mismos mocks: lo verificado sigue siendo qué
        // entradas de tiempo llegan a OpenProject, no por qué colaborador pasan.
        var useCase = new EndTaskSessionCommandImpl(
            repositoryMock.Object,
            new PendingTimeUploaderImpl(
                repositoryMock.Object,
                addTimeEntryMock.Object,
                activityOpServiceMock.Object),
            updateMock.Object,
            BuildUserManagerMock(addRandomSlackTime).Object,
            new FakeCurrentUser());

        return (useCase, repositoryMock, addTimeEntryMock, updateMock, activityOpServiceMock);
    }

    private static TaskEntity BuildTask(params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = 1,
        ProjectId = 1,
        StatusTaskId = 1,
        Name = "Create user module",
        Description = "desc",
        TasksTimeDetails = details.ToList()
    };

    [Fact]
    public async Task Execute_TaskNotFound_ThrowsArgumentException()
    {
        var (useCase, _, _, _, _) = BuildUseCase(null);
        var request = new EndTaskSessionRequest(99, 2, "no existe");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => useCase.Execute(request));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Execute_TaskWithoutTimeDetails_ThrowsInvalidOperationException()
    {
        var task = BuildTask();
        var (useCase, _, _, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "sin sesiones");

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.Execute(request));
    }

    [Fact]
    public async Task Execute_LastDetailAlreadyClosed_CallsAddTimeEntryAndSaves()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 12, 30, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, addMock, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "done");

        var result = await useCase.Execute(request);

        Assert.NotNull(result);
        Assert.True(detail.Uploaded);
        Assert.Equal(new DateTime(2026, 5, 1, 12, 30, 0), detail.EndTime);
        addMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r =>
            r.IdWorkPackage == 1 &&
            r.IdActivity == 2 &&
            r.Comment == "done" &&
            r.Hours == 2.5)), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.AtLeastOnce);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_LastDetailWithoutEndTime_SetsEndTimeAndUploads()
    {
        // Antes este test usaba StartTime = DateTime.Now para que la duración fuera de
        // milisegundos y así esquivar el margen aleatorio, que no era determinista. Con el
        // redondeo al cuarto de hora ya no hace falta: una duración realista da un resultado
        // exacto. (Y una sesión de milisegundos hoy redondea a cero y no se sube, que es lo
        // correcto — antes llegaba a OpenProject.)
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = DateTime.Now.AddMinutes(-30),
            EndTime = null,
            Uploaded = false
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, addMock, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "auto-cierre");

        await useCase.Execute(request);

        Assert.NotNull(detail.EndTime);
        Assert.True(detail.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_AddRandomSlackTimeEnabled_RedondeaAlSiguienteCuartoDeHora()
    {
        // 32 min trackeados -> el bloque siguiente es 45.
        var start = DateTime.Now.AddMinutes(-32);
        var detail = new TaskTimeDetail { Id = 1, StartTime = start, EndTime = null };
        var task = BuildTask(detail);
        var (useCase, _, _, _, _) = BuildUseCase(task, addRandomSlackTime: true);
        var request = new EndTaskSessionRequest(1, 2, "con redondeo");

        await useCase.Execute(request);

        // El resultado se calcula desde StartTime, así que es exacto y no depende de cuánto
        // tardó la llamada.
        Assert.Equal(start.AddMinutes(45), detail.EndTime);
    }

    [Fact]
    public async Task Execute_AddRandomSlackTimeEnabled_DuracionYaMultiploDe15_NoLaToca()
    {
        // El defecto que motivó el cambio: 30 min exactos recibían de 10 a 20 minutos extra
        // porque el algoritmo miraba TimeSpan.Minutes (el resto) en vez de la duración.
        var start = DateTime.Now.AddMinutes(-30);
        var detail = new TaskTimeDetail { Id = 1, StartTime = start, EndTime = null };
        var task = BuildTask(detail);
        var (useCase, _, _, _, _) = BuildUseCase(task, addRandomSlackTime: true);
        var request = new EndTaskSessionRequest(1, 2, "ya cae justo");

        await useCase.Execute(request);

        Assert.Equal(start.AddMinutes(30), detail.EndTime);
    }

    [Fact]
    public async Task Execute_AddRandomSlackTimeDisabled_UsesExactTrackedTime()
    {
        var detail = new TaskTimeDetail { Id = 1, StartTime = DateTime.Now.AddMinutes(-30), EndTime = null };
        var task = BuildTask(detail);
        var (useCase, _, _, _, _) = BuildUseCase(task, addRandomSlackTime: false);
        var request = new EndTaskSessionRequest(1, 2, "sin holgura");
        var before = DateTime.Now;

        await useCase.Execute(request);

        // Sin la holgura, EndTime debe quedar pegado al momento real de la llamada, no
        // desplazado varios minutos hacia adelante.
        Assert.True(detail.EndTime <= before.AddSeconds(5));
    }

    [Fact]
    public async Task Execute_WithNewStatusId_UpdatesWorkPackageAndTask()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        await useCase.Execute(request);

        Assert.Equal(5, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            1, 5, null, null, null, It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Execute_StatusUpdateFails_ThrowsTaskStatusTransitionExceptionAfterPersistingTimeEntry()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, repoMock, addMock, updateMock, _) = BuildUseCase(task);
        updateMock
            .Setup(x => x.Execute(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Error HTTP 422: {\"message\":\"Estado no es válido porque no existe ninguna transición.\"}"));
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        var ex = await Assert.ThrowsAsync<TaskStatusTransitionException>(() => useCase.Execute(request));

        Assert.Contains("Estado no es válido porque no existe ninguna transición.", ex.Message);
        Assert.Equal(1, task.StatusTaskId);
        Assert.True(detail.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Once);
        repoMock.Verify(x => x.SaveAsync(task), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_InvalidStatusTransition_IncludesAllowedStatusesSuggestion()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 1;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        updateMock
            .Setup(x => x.Execute(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidStatusTransitionException(
                "Estado no es válido porque no existe ninguna transición.",
                new List<string> { "New", "In progress", "Closed" }));
        var request = new EndTaskSessionRequest(1, 2, "cerrar tarea", NewStatusId: 5);

        var ex = await Assert.ThrowsAsync<TaskStatusTransitionException>(() => useCase.Execute(request));

        Assert.Contains("Estado no es válido porque no existe ninguna transición.", ex.Message);
        Assert.Contains("Desde el estado actual puedes cambiar directamente a: New, In progress, Closed.", ex.Message);
    }

    [Fact]
    public async Task Execute_WithoutNewStatusId_DoesNotCallUpdateWorkPackage()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 7;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "sin cambiar status");

        await useCase.Execute(request);

        Assert.Equal(7, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_WithNewStatusIdZero_DoesNotCallUpdateWorkPackage()
    {
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        task.StatusTaskId = 7;
        var (useCase, _, _, updateMock, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "status invalido", NewStatusId: 0);

        await useCase.Execute(request);

        Assert.Equal(7, task.StatusTaskId);
        updateMock.Verify(x => x.Execute(
            It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Execute_LastDetailAlreadyUploaded_DoesNotDuplicateTimeEntry()
    {
        // La sesión ya fue cerrada y subida (ej. por una pausa previa); al finalizar la tarea
        // no debe registrarse el tiempo nuevamente.
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = true
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, addMock, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "cerrar tras pausa");

        await useCase.Execute(request);

        Assert.True(detail.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Never);
        repoMock.Verify(x => x.SaveAsync(task), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Execute_PicksLatestTimeDetailByStartTime()
    {
        var oldest = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 4, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 1, 10, 0, 0),
            Uploaded = false
        };
        var middle = new TaskTimeDetail
        {
            Id = 2,
            StartTime = new DateTime(2026, 4, 15, 9, 0, 0),
            EndTime = new DateTime(2026, 4, 15, 10, 0, 0),
            Uploaded = false
        };
        var latest = new TaskTimeDetail
        {
            Id = 3,
            StartTime = new DateTime(2026, 5, 1, 9, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 10, 30, 0),
            Uploaded = false
        };
        // Orden mezclado a propósito: la lógica debe tomar el de StartTime más reciente.
        var task = BuildTask(middle, oldest, latest);
        var (useCase, _, addMock, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "ultima sesion");

        await useCase.Execute(request);

        // Al finalizar se registra TODO el tiempo pendiente, no solo la última sesión: las
        // pausadas con "guardar en local" quedaban con Uploaded = false y nunca llegaban a
        // OpenProject, que es justo lo que aparecía como "pendientes de subir" en el reporte.
        Assert.True(oldest.Uploaded);
        Assert.True(middle.Uploaded);
        Assert.True(latest.Uploaded);
        addMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Exactly(3));

        // La sesión más reciente se sigue identificando por StartTime, no por orden en la lista.
        addMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(r =>
            r.Hours == 1.5 && r.SpentOn == new DateOnly(2026, 5, 1))), Times.Once);
    }

    [Fact]
    public async Task Execute_BuscaLaTareaAcotadaAlUsuarioActual_NoSoloPorWorkPackageId()
    {
        // La PK real es (UserId, WorkPackageId): buscar solo por WorkPackageId podía finalizar
        // la sesión de OTRO tenant con el mismo id numérico.
        var detail = new TaskTimeDetail
        {
            Id = 1,
            StartTime = new DateTime(2026, 5, 1, 10, 0, 0),
            EndTime = new DateTime(2026, 5, 1, 11, 0, 0),
            Uploaded = false
        };
        var task = BuildTask(detail);
        var (useCase, repoMock, _, _, _) = BuildUseCase(task);
        var request = new EndTaskSessionRequest(1, 2, "scoped");

        await useCase.Execute(request);

        repoMock.Verify(x => x.GetByIdForUserAsync(1, "user-1", It.IsAny<bool>()), Times.Once);
    }
}
