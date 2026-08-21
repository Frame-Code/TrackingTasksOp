using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Application.Ports.UseCases.Reports;
using Application.Ports.UseCases.Settings;
using Application.Ports.UseCases.Tasks;
using Application.Ports.UseCases.TimeEntry;
using Application.Ports.UseCases.WorkPackages;
using Infrastructure.Adapters.Auth;
using Infrastructure.Adapters.Http;
using Infrastructure.Adapters.Repositories;
using Infrastructure.Adapters.Services;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Adapters.Services.Bot.Actions;
using Infrastructure.Adapters.UseCases;
using Infrastructure.Adapters.UseCases.Auth;
using Infrastructure.Adapters.UseCases.Reports;
using Infrastructure.Adapters.UseCases.Settings;
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
        collection.AddScoped<IGetWorkPackageCommand, GetWorkPackageCommandImpl>();
        collection.AddScoped<IStartTaskCommand, StartTaskCommandImpl>();
        collection.AddScoped<IEndTaskSessionCommand, EndTaskSessionCommandImpl>();
        collection.AddScoped<ICancelTaskSessionCommand, CancelTaskSessionCommandImpl>();
        collection.AddScoped<IPauseTaskCommand, PauseTaskCommandImpl>();
        collection.AddScoped<IPendingTimeUploader, PendingTimeUploaderImpl>();
        collection.AddScoped<IUploadPendingSessionsCommand, UploadPendingSessionsCommandImpl>();
        collection.AddScoped<IGetPendingSessionsSummaryQuery, GetPendingSessionsSummaryQueryImpl>();
        collection.AddScoped<ILogTimeCommand, LogTimeCommandImpl>();
        collection.AddScoped<IResumeTaskCommand, ResumeTaskCommandImpl>();
        collection.AddScoped<IAddTimeEntryCommand, AddTimeEntryCommandImpl>();
        collection.AddScoped<IRegisterLocalUserCommand, RegisterLocalUserCommandImpl>();
        collection.AddScoped<ILoginLocalUserCommand, LoginLocalUserCommandImpl>();
        collection.AddScoped<IUpdateApiKeyCommand, UpdateApiKeyCommandImpl>();
        collection.AddScoped<IGenerateDailyTaskReportCommand, GenerateDailyTaskReportCommandImpl>();
        collection.AddScoped<IGetUserSettingsQuery, GetUserSettingsQueryImpl>();
        collection.AddScoped<IUpdateNotificationSettingCommand, UpdateNotificationSettingCommandImpl>();
        collection.AddScoped<IUpdateTaskPreferencesCommand, UpdateTaskPreferencesCommandImpl>();
        collection.AddScoped<IUpdateAiApiKeyCommand, UpdateAiApiKeyCommandImpl>();

        //Services
        collection.AddScoped<IStatusOpService, StatusOpServiceImpl>();
        collection.AddScoped<IProjectOpService, ProjectOpServiceImpl>();
        collection.AddScoped<IActivityOpService, ActivityOpServiceImpl>();
        collection.AddScoped<IUserOpService, UserOpServiceImpl>();
        collection.AddScoped<ITimeEntryOpService, TimeEntryOpServiceImpl>();
        collection.AddScoped<IOpInstanceRepository, OpInstanceRepositoryImpl>();
        collection.AddScoped<IOpInstanceService, OpInstanceServiceImpl>();
        collection.AddScoped<IApiKeyEncryptorService, DataProtectionApiKeyEncryptorImpl>();
        // Scoped: memoiza la credencial de OpenProject por request (ver la clase).
        collection.AddScoped<Infrastructure.Adapters.Http.OpenProjectAuthHeaderProvider>();
        // Scoped: acumula los tiempos del request para la cabecera Server-Timing.
        collection.AddScoped<Infrastructure.Adapters.Http.RequestTimings>();
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

        // Bot - Groq adapter
        collection.AddScoped<IGroqApiClient, GroqApiClient>();
        collection.AddScoped<IAudioTranscriptionService, GroqTranscriptionClient>();
        collection.AddScoped<GroqAuthHeaderProvider>();
        collection.AddScoped<IAiUsageLimiter, AiUsageLimiterImpl>();
        collection.AddScoped<IBotIntentInterceptor, HeuristicIntentInterceptor>();
        collection.AddScoped<IOpenProjectEntityResolver, OpenProjectEntityResolver>();
        collection.AddScoped<IBotActionExecutor, BotActionExecutor>();
        collection.AddScoped<IBotActionHandler, ListProjectsActionHandler>();
        collection.AddScoped<IBotActionHandler, ListTasksActionHandler>();
        collection.AddScoped<IBotActionHandler, ListStatusesActionHandler>();
        collection.AddScoped<IBotActionHandler, StartTaskActionHandler>();
        collection.AddScoped<IBotActionHandler, CreateTaskActionHandler>();
        collection.AddScoped<IBotActionHandler, AssignUserToTaskActionHandler>();
        collection.AddScoped<IBotActionHandler, EndTaskSessionActionHandler>();
        collection.AddScoped<IBotActionHandler, UpdateTaskStatusActionHandler>();
        collection.AddScoped<IBotActionHandler, ListProjectUsersActionHandler>();
        collection.AddScoped<IBotActionHandler, UpdateProgressActionHandler>();
        collection.AddScoped<IBotActionHandler, UpdateTaskDatesActionHandler>();
        collection.AddScoped<IBotActionHandler, PauseTaskActionHandler>();
        collection.AddScoped<IBotActionHandler, ResumeTaskActionHandler>();

        // Infrastructure Clients
        var redisSettings = configuration.GetSection("RedisSettings").Get<RedisSettings>();
        if (redisSettings != null)
        {
            // AbortOnConnectFail=false: si Redis no está disponible en este instante (ej. corre en
            // WSL y todavía no arrancó), el multiplexer no tira una excepción que se lleve puesta
            // TODA la app — sigue reintentando en segundo plano. RedisConversationService ya maneja
            // los fallos de conexión en cada operación puntual (guardar/leer contexto del bot).
            var redisOptions = ConfigurationOptions.Parse(redisSettings.Configuration);
            redisOptions.AbortOnConnectFail = false;
            collection.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisOptions));
        }
        
        return collection;
    }

}
