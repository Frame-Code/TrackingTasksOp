using Application.Ports.UseCases.WorkPackages;
using Web.Infrastructure.Config.Settings;
using Web.Infrastructure.Services.UseCases.WorkPackages;

namespace Web.Infrastructure.Config.Extensions;
public static class ServicesExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection collection, IConfiguration configuration)
    {
        collection.AddKeyedSingleton<IApiSettings, OpenProjectSettings>(nameof(KeyService.OpenProjectSettings));
        collection.AddScoped<IListsWorkPackagesCommand, ListsWorkPackagesCommandImpl>();
        return collection;
    }

}
