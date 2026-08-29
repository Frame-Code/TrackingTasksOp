using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Dto.WorkPackages;
using Application.Ports.Auth;
using Infrastructure.Adapters.Repositories;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Adapters.UseCases.TimeEntry;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Infrastructure.DataAccess;
using Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Integration;

/// <summary>
/// Pruebas de integración end-to-end del bot (StartTaskActionHandler) creando una tarea nueva.
/// Requieren OpenProject (docker "oplocal-web-1", puerto 8081) y SQL Server (docker "sqlserver_dev",
/// puerto 1433, base "TrackingTasksDb") corriendo localmente.
/// </summary>
[Trait("Category", "Integration")]
public class StartTaskBotIntegrationTests
{
    private const string OpenProjectBaseUrl = "http://localhost:8081";
    private static readonly string ApiKey =
        Environment.GetEnvironmentVariable("OPENPROJECT_TEST_API_KEY") ?? "YOUR_OPENPROJECT_API_KEY";
    private static readonly string SqlConnectionString =
        Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION_STRING")
        ?? "Host=localhost;Database=TrackingTasksDb;Username=postgres;Password=YOUR_POSTGRES_PASSWORD";

    // Usuario seed ya asociado a la instancia OpenProject .
    private static readonly string SeedUserId =
        Environment.GetEnvironmentVariable("TEST_SEED_USER_ID") ?? "YOUR_SEED_USER_ID";
    private const int SeedOpenProjectInstanceId = 2;

    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => SeedUserId;
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => OpenProjectBaseUrl;
        public override int? OpenProjectInstanceId => SeedOpenProjectInstanceId;
        public override int? OpenProjectUserId => null;
    }

    private static IHttpClientFactory BuildRealHttpClientFactory()
    {
        var client = new HttpClient { BaseAddress = new Uri(OpenProjectBaseUrl) };
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{ApiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(KeyedServicesNames.OpenProjectHttpClientName)).Returns(client);
        return factoryMock.Object;
    }

    private static TrackingTasksDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseNpgsql(SqlConnectionString)
            .Options;
        return new TrackingTasksDbContext(options);
    }

    [Fact]
    public async Task StartTask_ViaBot_WithNewWorkPackage_ShouldCreateWorkPackageAndPersistTaskInDb()
    {
        var factory = BuildRealHttpClientFactory();
        var projectOpService = new ProjectOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance);
        var statusOpService = new StatusOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance);
        var userOpService = new UserOpServiceImpl(factory, NullLogger<UserOpServiceImpl>.Instance);
        var entityResolver = new OpenProjectEntityResolver(projectOpService, statusOpService, userOpService, new FakeCurrentUser());
        var createWorkPackageCommand = new CreateWorkPackageCommandImpl(factory, NullLogger<CreateWorkPackageCommandImpl>.Instance);
        var addTimeEntryCommand = new AddTimeEntryCommandImpl(factory, NullLogger<AddTimeEntryCommandImpl>.Instance);

        using var dbContext = BuildDbContext();
        var taskRepository = new TaskRepositoryImpl(dbContext);
        var projectRepository = new ProjectRepositoryImpl(dbContext);

        var startTaskCommand = new StartTaskCommandImpl(
            taskRepository,
            projectRepository,
            createWorkPackageCommand,
            projectOpService,
            new FakeCurrentUser());

        var handler = new StartTaskActionHandler(startTaskCommand, statusOpService, entityResolver, createWorkPackageCommand);

        var projectId = await entityResolver.ResolveProjectId("sexoanal");
        Assert.NotNull(projectId);

        var requiredFields = await createWorkPackageCommand.GetRequiredCustomFieldsAsync(projectId!.Value);
        var customFields = requiredFields.ToDictionary(f => f.Name, f => f.AllowedValues.First().Value);

        var taskName = $"Tarea bot integración {Guid.NewGuid():N}";
        var action = new GroqAction
        {
            Action = "start_task",
            Params = new Dictionary<string, object>
            {
                ["projectName"] = "sexoanal",
                ["statusName"] = "New",
                ["name"] = taskName,
                ["customFields"] = JsonSerializer.SerializeToElement(customFields)
            }
        };

        int createdWorkPackageId = 0;
        try
        {
            var result = await handler.ExecuteAsync(action, null);

            Assert.Contains("seguimiento iniciado", result);
            Assert.Contains(taskName, result);

            var match = Regex.Match(result, @"ID:\s*(\d+)");
            Assert.True(match.Success, $"No se encontró el ID del work package en la respuesta: {result}");
            createdWorkPackageId = int.Parse(match.Groups[1].Value);

            var savedTask = await dbContext.Tasks
                .AsNoTracking()
                .Include(t => t.TasksTimeDetails)
                .FirstOrDefaultAsync(t => t.UserId == SeedUserId && t.WorkPackageId == createdWorkPackageId);

            Assert.NotNull(savedTask);
            Assert.Equal(taskName, savedTask!.Name);
            Assert.Equal(projectId.Value, savedTask.ProjectId);
            Assert.Equal(SeedOpenProjectInstanceId, savedTask.OpenProjectInstanceId);

            var openSession = savedTask.TasksTimeDetails.SingleOrDefault(d => d.EndTime == null);
            Assert.NotNull(openSession);
            Assert.Equal(SeedUserId, openSession!.UserId);
        }
        finally
        {
            if (createdWorkPackageId > 0)
            {
                var entity = await dbContext.Tasks
                    .Include(t => t.TasksTimeDetails)
                    .FirstOrDefaultAsync(t => t.UserId == SeedUserId && t.WorkPackageId == createdWorkPackageId);

                if (entity != null)
                {
                    dbContext.TasksTimeDetails.RemoveRange(entity.TasksTimeDetails);
                    dbContext.Tasks.Remove(entity);
                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
