using Application.Dto.Conversation;
using Application.Dto.Tasks;
using Application.Dto.WorkPackages;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.TrackingTasksEntities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;
using Xunit;
using System.Reflection;
using Google.GenAI.Types;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class GeminiIntentServiceTests
{
    private readonly Mock<ILogger<GeminiIntentService>> _loggerMock;
    private readonly Mock<IConversationContextService> _conversationContextMock;
    private readonly Mock<IStartTaskCommand> _startTaskMock;
    private readonly Mock<IEndTaskSessionCommand> _endTaskSessionMock;
    private readonly Mock<IListsWorkPackagesCommand> _listsWorkPackagesMock;
    private readonly Mock<IUpdateWorkPackageCommand> _updateWorkPackageMock;
    private readonly Mock<IProjectOpService> _projectOpMock;
    private readonly Mock<IStatusOpService> _statusOpMock;
    private readonly Mock<IActivityOpService> _activityOpMock;
    private readonly Mock<IUserOpService> _userOpMock;
    private readonly IOptions<GeminiSettings> _settings;

    public GeminiIntentServiceTests()
    {
        _loggerMock = new Mock<ILogger<GeminiIntentService>>();
        _conversationContextMock = new Mock<IConversationContextService>();
        _startTaskMock = new Mock<IStartTaskCommand>();
        _endTaskSessionMock = new Mock<IEndTaskSessionCommand>();
        _listsWorkPackagesMock = new Mock<IListsWorkPackagesCommand>();
        _updateWorkPackageMock = new Mock<IUpdateWorkPackageCommand>();
        _projectOpMock = new Mock<IProjectOpService>();
        _statusOpMock = new Mock<IStatusOpService>();
        _activityOpMock = new Mock<IActivityOpService>();
        _userOpMock = new Mock<IUserOpService>();

        _settings = Options.Create(new GeminiSettings
        {
            ProjectId = "test-project",
            Location = "us-central1",
            Model = "gemini-1.5-pro"
        });
    }

    private GeminiIntentService CreateService()
    {
        return new GeminiIntentService(
            _loggerMock.Object,
            _settings,
            _conversationContextMock.Object,
            _startTaskMock.Object,
            _endTaskSessionMock.Object,
            _listsWorkPackagesMock.Object,
            _updateWorkPackageMock.Object,
            _projectOpMock.Object,
            _statusOpMock.Object,
            _activityOpMock.Object,
            _userOpMock.Object
        );
    }

    [Fact]
    public async Task ExecuteFunctionAsync_UnknownFunction_ReturnsError()
    {
        var service = CreateService();
        var call = new FunctionCall { Name = "non_existent_function" };

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        // Usar reflexión para obtener la propiedad 'error' del objeto anónimo
        var errorProp = result.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        var errorValue = errorProp!.GetValue(result)?.ToString();
        Assert.Contains("no implementada", errorValue);
    }

    [Fact]
    public async Task ExecuteFunctionAsync_MissingRequiredArg_ReturnsError()
    {
        var service = CreateService();
        var call = new FunctionCall { Name = "list_work_packages", Args = new Dictionary<string, object>() };

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        var errorProp = result.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        var errorValue = errorProp!.GetValue(result)?.ToString();
        Assert.Contains("Missing required argument", errorValue);
    }

    [Fact]
    public async Task ExecuteFunctionAsync_StartTask_CallsCommand()
    {
        var service = CreateService();
        var call = new FunctionCall 
        { 
            Name = "start_task", 
            Args = new Dictionary<string, object> 
            { 
                ["projectId"] = 1,
                ["statusId"] = 2,
                ["name"] = "New Task",
                ["description"] = "Desc"
            } 
        };

        _startTaskMock.Setup(x => x.Execute(It.IsAny<StarTaskRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 456, Name = "New Task" });

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        _startTaskMock.Verify(x => x.Execute(It.Is<StarTaskRequest>(r => r.Name == "New Task" && r.ProjectId == 1)), Times.Once);
    }

    [Fact]
    public async Task ExecuteFunctionAsync_EndTaskSession_CallsCommand()
    {
        var service = CreateService();
        var call = new FunctionCall 
        { 
            Name = "end_task_session", 
            Args = new Dictionary<string, object> 
            { 
                ["workPackageId"] = 123,
                ["activityId"] = 5,
                ["comment"] = "Done"
            } 
        };

        _endTaskSessionMock.Setup(x => x.Execute(It.IsAny<EndTaskSessionRequest>()))
            .ReturnsAsync(new Domain.Entities.TrackingTasksEntities.Task { WorkPackageId = 123, Name = "Test Task" });

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        _endTaskSessionMock.Verify(x => x.Execute(It.Is<EndTaskSessionRequest>(r => r.WorkPackageId == 123 && r.ActivityId == 5)), Times.Once);
    }

    [Fact]
    public async Task ExecuteFunctionAsync_AssignUser_CallsCommand()
    {
        var service = CreateService();
        var call = new FunctionCall 
        { 
            Name = "assign_user_to_task", 
            Args = new Dictionary<string, object> 
            { 
                ["workPackageId"] = 123,
                ["assigneeName"] = "John Doe"
            } 
        };

        _userOpMock.Setup(x => x.FindByName("John Doe"))
            .ReturnsAsync(new Domain.Entities.OpenProjectEntities.User.User { Id = 789, Name = "John Doe" });

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        _updateWorkPackageMock.Verify(x => x.Execute(123, null, 789, null), Times.Once);
    }

    [Fact]
    public async Task ExecuteFunctionAsync_AssignUser_UserNotFound_ReturnsError()
    {
        var service = CreateService();
        var call = new FunctionCall 
        { 
            Name = "assign_user_to_task", 
            Args = new Dictionary<string, object> 
            { 
                ["workPackageId"] = 123,
                ["assigneeName"] = "Unknown User"
            } 
        };

        _userOpMock.Setup(x => x.FindByName("Unknown User"))
            .ReturnsAsync((Domain.Entities.OpenProjectEntities.User.User)null!);

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        var errorProp = result.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Contains("No se encontró al usuario", errorProp!.GetValue(result)!.ToString());
    }

    [Fact]
    public async Task ExecuteFunctionAsync_InvalidArgType_HandlesError()
    {
        var service = CreateService();
        // Intentar pasar un string donde se espera un int
        var call = new FunctionCall 
        { 
            Name = "list_work_packages", 
            Args = new Dictionary<string, object> { ["projectId"] = "abc" } 
        };

        var method = typeof(GeminiIntentService).GetMethod("ExecuteFunctionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task<object>)method!.Invoke(service, new object[] { call })!;
        var result = await task;

        Assert.NotNull(result);
        var errorProp = result.GetType().GetProperty("error");
        Assert.NotNull(errorProp); // Debería capturar el FormatException o InvalidCastException
    }

}

