using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.WorkPackages;
using Web.Infrastructure.Adapters.Repositories;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Adapters.UseCases.Tasks;
using Web.Infrastructure.Adapters.UseCases.WorkPackages;
using Web.Infrastructure.Config.Settings;

namespace Web.Infrastructure.Config.Extensions;
public static class ServicesExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection collection, IConfiguration configuration)
    {
        //Settings
        collection.AddKeyedSingleton<IApiSettings, OpenProjectSettings>(nameof(KeyService.OpenProjectSettings));
        
        //Use cases
        collection.AddScoped<IListsWorkPackagesCommand, ListsWorkPackagesCommandImpl>();
        collection.AddScoped<IStartTaskCommand, StartTaskCommandImpl>();
        collection.AddScoped<IEndTaskSessionCommand, EndTaskSessionCommandImpl>();
        
        //Services
        collection.AddScoped<IStatusOpService, StatusOpServiceImpl>();
        collection.AddScoped<IProjectOpService, ProjectOpServiceImpl>();
        
        //Repositories
        collection.AddScoped<IStatusTaskRepository, StatusTaskRepositoryImpl>();
        collection.AddScoped<ITaskRepository, TaskRepositoryImpl>();
        collection.AddScoped<IProjectRepository, ProjectRepositoryImpl>();
        
        return collection;
    }

}
