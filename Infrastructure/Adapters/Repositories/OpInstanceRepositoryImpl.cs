using System.Xml;
using Application.Dto.OpInstance;
using Application.Ports.Repositories;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
using Infrastructure.Extensions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Repositories;

public class OpInstanceRepositoryImpl(
    ILogger<OpInstanceRepositoryImpl> logger,
    TrackingTasksDbContext context
    ) : IOpInstanceRepository
{
    public async Task Save(OpInstanceDto dto)
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