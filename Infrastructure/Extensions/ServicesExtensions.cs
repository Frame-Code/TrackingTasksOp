using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Auth;
using Infrastructure.Adapters.Http;
using Infrastructure.Adapters.Repositories;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.UseCases;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.Adapters.UseCases.TimeEntry;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Infrastructure.Extensions;
public static class ServicesExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection collection, IConfiguration configuration)
    {
        //Singletons
        collection.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        //Current user
        collection.AddScoped<CurrentUser, HttpContextCurrentUser>();
        
        //Settings
        collection.Configure<RedisSettings>(configuration.GetSection("RedisSettings"));
        var aiModel = configuration.GetSection("AIModel")
            .GetChildren()
            .First();
        collection.Configure<GroqSettings>(aiModel);
        //collection.Configure<GeminiSettings>(aiModel);
        //collection.Configure<OllamaSettings>(aiModel);

        //Use cases
        collection.AddScoped<IListsWorkPackagesCommand, ListsWorkPackagesCommandImpl>();
        collection.AddScoped<ICreateWorkPackageCommand, CreateWorkPackageCommandImpl>();
        collection.AddScoped<IUpdateWorkPackageCommand, UpdateWorkPackageCommandImpl>();
        collection.AddScoped<IStartTaskCommand, StartTaskCommandImpl>();
        collection.AddScoped<IEndTaskSessionCommand, EndTaskSessionCommandImpl>();
        collection.AddScoped<ICancelTaskSessionCommand, CancelTaskSessionCommandImpl>();
        collection.AddScoped<IAddTimeEntryCommand, AddTimeEntryCommandImpl>();
        collection.AddScoped<IRegisterLocalUserCommand, RegisterLocalUserCommandImpl>();
        collection.AddScoped<ILoginLocalUserCommand, LoginLocalUserCommandImpl>();
        
        //Services
        collection.AddScoped<IStatusOpService, StatusOpServiceImpl>();
        collection.AddScoped<IProjectOpService, ProjectOpServiceImpl>();
        collection.AddScoped<IActivityOpService, ActivityOpServiceImpl>();
        collection.AddScoped<IUserOpService, UserOpServiceImpl>();
        collection.AddScoped<IApiKeyEncryptorService, DataProtectionApiKeyEncryptorImpl>();
        collection.AddScoped<IApiKeyValidatorService, ApiKeyValidatorServiceImpl>();
        collection.AddScoped<IAuthAuditLogger, AuthAuditLoggerImpl>();
        collection.AddScoped<IInitializerInstanceService, InitializerInstanceServiceImpl>();
        collection.AddKeyedScoped<BaseUrlService, OpenProjectUrlServiceImpl>(KeyedServicesNames.OpenProjectUrlService);
        
        //Repositories
        collection.AddScoped<IStatusTaskRepository, StatusTaskRepositoryImpl>();
        collection.AddScoped<ITaskRepository, TaskRepositoryImpl>();
        collection.AddScoped<IProjectRepository, ProjectRepositoryImpl>();

        // AI Services
        collection.AddScoped<IAiIntentService, GroqIntentService>();
        collection.AddScoped<IConversationContextService, RedisConversationService>();
        
        // Infrastructure Clients
        var redisSettings = configuration.GetSection("RedisSettings").Get<RedisSettings>();
        if (redisSettings != null)
        {
            collection.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(redisSettings.Configuration));
        }
        
        return collection;
    }

}
