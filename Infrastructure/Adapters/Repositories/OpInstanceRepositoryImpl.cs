using Application.Dto.OpInstance;
using Application.Ports.Repositories;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
using Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Repositories;

public class OpInstanceRepositoryImpl(
    ILogger<OpInstanceRepositoryImpl> logger,
    TrackingTasksDbContext context
    ) : IOpInstanceRepository
{
    public async Task<IEnumerable<ListsOpInstanceDto>> Lists()
    {
        return await context.OpenProjectInstances
            .AsNoTracking()
            .Select(x => new ListsOpInstanceDto(x.Id, x.BaseUrl, x.Alias ?? "-", x.OAuthClientId != null))
            .ToListAsync();
    }

    public async Task<GetOpInstance?> GetOpInstance(int instanceId)
    {
        var instance = await context.OpenProjectInstances.FirstOrDefaultAsync(x => x.Id == instanceId);
        return instance == null 
            ? null 
            : new GetOpInstance(instance.BaseUrl, instance.OAuthClientId, instance.EncryptedOAuthClientSecret);
    }

    public async Task Save(SaveOpInstanceDto dto)
    {
        var instance = context.OpenProjectInstances.FirstOrDefault(x => x.Id == dto.idInstance);
        if (instance is null)
        {
            logger.LogError($"Error fatal: no se encontró la instancia de openproject con id: {dto.idInstance}");
            throw new OpInstanceNotFoundException("Error fatal: no se encontró la instancia de openproject, por favor intente de nuevo");
        }

        instance.Alias = dto.Alias;
        instance.EncryptedOAuthClientSecret = dto.ClientSecret;
        instance.OAuthClientId = dto.ClientId;
        instance.OAuthConnectedAt = DateTime.Now;
        await context.AddOrUpdateAsync(instance);
        await context.SaveChangesAsync();
    }
}