using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.TimeEntry;
using Domain.Entities.OpenProjectEntities.Activity;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Services;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class PendingTimeUploaderImplTests
{
    private readonly Mock<ITaskRepository> _repositoryMock = new();
    private readonly Mock<IAddTimeEntryCommand> _addTimeEntryMock = new();
    private readonly Mock<IActivityOpService> _activityOpServiceMock = new();

    public PendingTimeUploaderImplTests()
    {
        _repositoryMock.Setup(x => x.SaveAsync(It.IsAny<TaskEntity>())).ReturnsAsync((TaskEntity t) => t);
        _addTimeEntryMock.Setup(x => x.Execute(It.IsAny<AddTimeEntryRequest>())).Returns(Task.CompletedTask);
        _activityOpServiceMock.Setup(x => x.Lists(It.IsAny<int>()))
            .ReturnsAsync(new List<ActivityAllowedValue> { new() { Id = 99, Name = "Development" } });
    }

    private PendingTimeUploaderImpl BuildUploader() => new(
        _repositoryMock.Object, _addTimeEntryMock.Object, _activityOpServiceMock.Object);

    private static TaskEntity BuildTask(params TaskTimeDetail[] details) => new()
    {
        WorkPackageId = 42, UserId = "user-1", ProjectId = 1, StatusTaskId = 1,
        Name = "Task", TasksTimeDetails = details.ToList()
    };

    private static TaskTimeDetail Closed(int id, DateTime start, double hours, bool uploaded = false) => new()
    {
        Id = id, StartTime = start, EndTime = start.AddHours(hours), Uploaded = uploaded
    };

    [Fact]
    public async Task UploadPendingAsync_SubeTodasLasPendientesYLasMarca()
    {
        var a = Closed(1, new DateTime(2026, 8, 5, 9, 0, 0), 2);
        var b = Closed(2, new DateTime(2026, 8, 11, 9, 0, 0), 1);
        var task = BuildTask(a, b);

        var uploaded = await BuildUploader().UploadPendingAsync(task);

        Assert.Equal(2, uploaded);
        Assert.True(a.Uploaded);
        Assert.True(b.Uploaded);
        _addTimeEntryMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UploadPendingAsync_UsaLaFechaRealDeCadaSesion()
    {
        var task = BuildTask(Closed(1, new DateTime(2026, 8, 5, 9, 0, 0), 2));

        await BuildUploader().UploadPendingAsync(task);

        _addTimeEntryMock.Verify(x => x.Execute(It.Is<AddTimeEntryRequest>(
            r => r.SpentOn == new DateOnly(2026, 8, 5) && r.Hours == 2 && r.IdWorkPackage == 42)), Times.Once);
    }

    [Fact]
    public async Task UploadPendingAsync_IgnoraLasYaSubidasYLasSesionesAbiertas()
    {
        var yaSubida = Closed(1, new DateTime(2026, 8, 5, 9, 0, 0), 2, uploaded: true);
        var abierta = new TaskTimeDetail { Id = 2, StartTime = new DateTime(2026, 8, 12, 9, 0, 0), EndTime = null };
        var task = BuildTask(yaSubida, abierta);

        var uploaded = await BuildUploader().UploadPendingAsync(task);

        Assert.Equal(0, uploaded);
        _addTimeEntryMock.Verify(x => x.Execute(It.IsAny<AddTimeEntryRequest>()), Times.Never);
    }

    [Fact]
    public async Task UploadPendingAsync_SiFallaAMitad_LoYaSubidoQuedaPersistidoYNoSeDuplica()
    {
        var primera = Closed(1, new DateTime(2026, 8, 5, 9, 0, 0), 2);
        var segunda = Closed(2, new DateTime(2026, 8, 6, 9, 0, 0), 3);
        var task = BuildTask(primera, segunda);

        _addTimeEntryMock
            .Setup(x => x.Execute(It.Is<AddTimeEntryRequest>(r => r.Hours == 3)))
            .ThrowsAsync(new Exception("OpenProject rechazó la entrada"));

        await Assert.ThrowsAsync<Exception>(() => BuildUploader().UploadPendingAsync(task));

        // La primera quedó marcada y guardada: un reintento no la vuelve a registrar.
        Assert.True(primera.Uploaded);
        Assert.False(segunda.Uploaded);
        _repositoryMock.Verify(x => x.SaveAsync(task), Times.Once);
    }
}
