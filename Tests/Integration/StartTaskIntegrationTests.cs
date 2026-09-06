using System.Net.Http.Headers;
using System.Text;
using Application.Dto.WorkPackages;
using Application.Ports.Auth;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.Services.Bot;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Integration;

/// <summary>
/// Pruebas de integración contra la instancia local de OpenProject (docker, puerto 8081).
/// Requieren que el contenedor "oplocal-web-1" esté levantado.
/// </summary>
[Trait("Category", "Integration")]
public class StartTaskIntegrationTests
{
    private const string OpenProjectBaseUrl = "http://localhost:8081";
    private static readonly string ApiKey =
        Environment.GetEnvironmentVariable("OPENPROJECT_TEST_API_KEY") ?? "YOUR_OPENPROJECT_API_KEY";

    private class FakeCurrentUser : CurrentUser
    {
        public override string? UserId => "user-1";
        public override bool IsAuthenticated => true;
        public override string? OpenProjectInstanceUrl => OpenProjectBaseUrl;
        public override int? OpenProjectInstanceId => 2;
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

    [Fact]
    public async Task ResolveUserId_WithProjectScope_ShouldUseAvailableAssigneesAndSucceed()
    {
        // Antes: ResolveUserId(name) llamaba a /api/v3/users (admin-only) y daba 403.
        // Ahora: si se indica el proyecto, se usa /api/v3/projects/{id}/available_assignees,
        // accesible para cualquier miembro del proyecto.
        var factory = BuildRealHttpClientFactory();
        var resolver = new OpenProjectEntityResolver(
            new ProjectOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new StatusOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new UserOpServiceImpl(factory, NullLogger<UserOpServiceImpl>.Instance),
            Mock.Of<IListsWorkPackagesCommand>(),
            Mock.Of<IGetWorkPackageCommand>(),
            new FakeCurrentUser());

        var projectId = await resolver.ResolveProjectId("sexitoanalsito");
        Assert.NotNull(projectId);

        var userId = await resolver.ResolveUserId("Stin Sanchez", projectId);

        Assert.Equal(30, userId);
    }

    [Fact]
    public async Task ResolveUserId_WithoutProjectScope_StillReturns403_BecauseUsersEndpointIsAdminOnly()
    {
        // Documenta el comportamiento legado (sin projectId): /api/v3/users sigue siendo admin-only.
        var factory = BuildRealHttpClientFactory();
        var resolver = new OpenProjectEntityResolver(
            new ProjectOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new StatusOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new UserOpServiceImpl(factory, NullLogger<UserOpServiceImpl>.Instance),
            Mock.Of<IListsWorkPackagesCommand>(),
            Mock.Of<IGetWorkPackageCommand>(),
            new FakeCurrentUser());

        var ex = await Assert.ThrowsAsync<Exception>(() => resolver.ResolveUserId("Stin Sanchez"));

        Assert.Contains("403", ex.Message);
        Assert.Contains("MissingPermission", ex.Message);
    }

    [Fact]
    public async Task GetRequiredCustomFields_OnRealOpenProject_ShouldReturnAreaAndModulo()
    {
        // El proyecto eProduction (tipo "Task") tiene 2 campos personalizados obligatorios:
        // "Area" (customField3) y "Modulo" (customField5), ambos de tipo CustomOption.
        var factory = BuildRealHttpClientFactory();
        var resolver = new OpenProjectEntityResolver(
            new ProjectOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new StatusOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new UserOpServiceImpl(factory, NullLogger<UserOpServiceImpl>.Instance),
            Mock.Of<IListsWorkPackagesCommand>(),
            Mock.Of<IGetWorkPackageCommand>(),
            new FakeCurrentUser());
        var createCommand = new CreateWorkPackageCommandImpl(factory, NullLogger<CreateWorkPackageCommandImpl>.Instance);

        var projectId = await resolver.ResolveProjectId("sexitoanalsito");
        Assert.NotNull(projectId);

        var requiredFields = await createCommand.GetRequiredCustomFieldsAsync(projectId!.Value);

        Assert.Contains(requiredFields, f => f.Key == "customField3" && f.Name == "Area" && f.AllowedValues.Count > 0);
        Assert.Contains(requiredFields, f => f.Key == "customField5" && f.Name == "Modulo" && f.AllowedValues.Count > 0);
    }

    [Fact]
    public async Task CreateWorkPackage_OnRealOpenProject_WithRequiredCustomFields_ShouldCreateTask()
    {
        // Con el fix del bug 422: resolvemos los campos personalizados obligatorios
        // (Area/Modulo) antes de crear la tarea, igual que haría StartTaskActionHandler
        // tras recibir la respuesta del usuario.
        var factory = BuildRealHttpClientFactory();
        var resolver = new OpenProjectEntityResolver(
            new ProjectOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new StatusOpServiceImpl(factory, NullLogger<StatusOpServiceImpl>.Instance),
            new UserOpServiceImpl(factory, NullLogger<UserOpServiceImpl>.Instance),
            Mock.Of<IListsWorkPackagesCommand>(),
            Mock.Of<IGetWorkPackageCommand>(),
            new FakeCurrentUser());
        var createCommand = new CreateWorkPackageCommandImpl(factory, NullLogger<CreateWorkPackageCommandImpl>.Instance);

        var projectId = await resolver.ResolveProjectId("sexoanal");
        var statusId = await resolver.ResolveStatusId("New");
        Assert.NotNull(projectId);
        Assert.NotNull(statusId);

        var requiredFields = await createCommand.GetRequiredCustomFieldsAsync(projectId!.Value);
        var customFieldOptionIds = requiredFields.ToDictionary(f => f.Key, f => f.AllowedValues.First().Id);

        var request = new CreateWorkPackageRequest(
            Subject: $"Tarea de integración {Guid.NewGuid():N}",
            ProjectId: projectId!.Value,
            StatusId: statusId,
            CustomFieldOptionIds: customFieldOptionIds);

        var workPackage = await createCommand.Execute(request);

        Assert.True(workPackage.Id > 0);
        Assert.Equal(request.Subject, workPackage.Subject);
    }
}
