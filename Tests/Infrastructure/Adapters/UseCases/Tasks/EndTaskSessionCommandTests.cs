using Application.Dto.Tasks;
using Application.Dto.TimeEntry;
using Application.Ports.Repositories;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Moq;
using Task = System.Threading.Tasks.Task;
using TaskEntity = Domain.Entities.TrackingTasksEntities.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class EndTaskSessionCommandTests
{
    private EndTaskSessionCommandImpl BuildUseCase(TaskEntity entity, EndTaskSessionRequest request, Task addTimeEntryTask, Task updateWorkPackageTask)
    {
        var repositoryMock = new Mock<ITaskRepository>();
        repositoryMock
            .Setup(x => x.GetByIdAsync(entity.WorkPackageId, It.IsAny<bool>()))
            .ReturnsAsync(entity);

        var addTimeEntryRequest = new AddTimeEntryRequest(request.WorkPackageId, request.ActivityId, 5.3, request.Comment);
        var addTimeEntryMock = new Mock<IAddTimeEntry>();
        addTimeEntryMock
            .Setup(x => x.Execute(addTimeEntryRequest))
            .Returns(addTimeEntryTask);

        var updateWorkPackageMock= new Mock<IUpdateWorkPackageCommand>();
        updateWorkPackageMock
            .Setup(x => x.Execute(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(updateWorkPackageTask);
        
        return new EndTaskSessionCommandImpl(repositoryMock.Object, addTimeEntryMock.Object, updateWorkPackageMock.Object);
    }

    //[Fact]
    public async Task Execute_SuccessfulRequest_ReturnTaskEntity()
    {
        var request = new EndTaskSessionRequest(1, 2, "Module users completed");
        var task = new TaskEntity
        {
            WorkPackageId = 1,
            ProjectId = 1,
            StatusTaskId = 1,
            Description = "Module users completed",
            Name = "Create user module in infrastructure layer",
            TasksTimeDetails = []
        };
        var useCase = BuildUseCase(null, null, null, null);
    }
    
}