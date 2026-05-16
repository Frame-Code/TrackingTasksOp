using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Dto.Auth;
using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Project;
using Domain.Entities.OpenProjectEntities.Status;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Project = Domain.Entities.TrackingTasksEntities.Project;
using Task = System.Threading.Tasks.Task;

namespace Infrastructure.Adapters.Services;

public class InitializerInstanceServiceImpl(
    TrackingTasksDbContext db,
    IHttpClientFactory httpClientFactory) : IInitializerInstanceService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    public async Task InitializeAsync(InitializeInstanceRequest request, CancellationToken ct)
    {
        var client = BuildClient(request.OpenProjectInstanceUrl, request.ApiKey);

        // Statuses
        var statusResponse = await client.GetAsync("/api/v3/statuses", ct);
        if (!statusResponse.IsSuccessStatusCode)
            throw new InitializerInstanceException("No se pudieron obtener los estados de la instancia registrada.");

        var statusJson = await statusResponse.Content.ReadAsStringAsync(ct);
        var statusesOp = JsonSerializer.Deserialize<StatusCollection>(statusJson, JsonOptions)
            ?.Embedded?.Elements ?? [];

        if (statusesOp.Count == 0)
            throw new InitializerInstanceException("No se encontraron estados de tareas en la instancia registrada, intente de nuevo.");

        // Projects
        var projectResponse = await client.GetAsync("/api/v3/projects", ct);
        if (!projectResponse.IsSuccessStatusCode)
            throw new InitializerInstanceException("No se pudieron obtener los proyectos de la instancia registrada.");

        var projectJson = await projectResponse.Content.ReadAsStringAsync(ct);
        var projects = JsonSerializer.Deserialize<ProjectCollection>(projectJson, JsonOptions)
            ?.Embedded?.Projects ?? [];

        if (projects.Count == 0)
            throw new InitializerInstanceException("No se encontraron proyectos en la instancia registrada, intente de nuevo.");

        // Filtrar los que ya existen localmente
        var statusLocal = await db.StatusTasks
            .Where(x => x.OpenProjectInstanceId == request.OpenProjectInstanceId)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var statusTasks = statusesOp
            .Where(x => !statusLocal.Contains(x.Name))
            .Select(x => new StatusTask
            {
                Id = x.Id,
                IsClosed = x.IsClosed,
                Name = x.Name,
                OpenProjectInstanceId = request.OpenProjectInstanceId
            })
            .ToList();

        var projectsLocal = await db.Projects
            .Where(x => x.OpenProjectInstanceId == request.OpenProjectInstanceId)
            .Select(x => x.Name)
            .ToListAsync(ct);

        var projectsToSave = projects
            .Where(x => !projectsLocal.Contains(x.Name))
            .Select(x => new Project
            {
                Id = x.Id,
                Name = x.Name,
                Identifier = x.Identifier,
                IsActive = x.IsActive,
                OpenProjectInstanceId = request.OpenProjectInstanceId
            })
            .ToList();

        var migration = new MigrationData
        {
            Description = $"Tables migrated: {db.GetTableName<StatusTask>()}, {db.GetTableName<Project>()}. User owner: id:{request.UserId}, username:{request.Username}",
            Name = "TrackingTasksOp Migration Data",
            OpenProjectInstanceId = request.OpenProjectInstanceId,
            UserId = request.UserId
        };

        await db.StatusTasks.AddRangeAsync(statusTasks, ct);
        await db.Projects.AddRangeAsync(projectsToSave, ct);
        await db.MigrationsData.AddAsync(migration, ct);
        await db.SaveChangesAsync(ct);
    }

    private HttpClient BuildClient(string instanceUrl, string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(instanceUrl.TrimEnd('/'));
        client.Timeout = TimeSpan.FromSeconds(20);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"apikey:{apiKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
