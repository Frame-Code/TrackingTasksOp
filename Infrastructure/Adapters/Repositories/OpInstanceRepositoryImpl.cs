using Application.Dto.OpInstance;
using Application.Ports.Repositories;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
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

        // El alias es como el usuario busca su organización en el selector de login OAuth:
        // si dos orgs comparten alias, no hay forma de distinguirlas ahí. Se excluye la propia
        // instancia para no bloquear que un admin la vuelva a conectar con el mismo alias.
        var aliasTaken = context.OpenProjectInstances.Any(x =>
            x.Id != dto.idInstance && x.Alias != null && x.Alias.ToLower() == dto.Alias.ToLower());
        if (aliasTaken)
            throw new DuplicateAliasException($"Ya existe una organización conectada con el alias '{dto.Alias}'. Elegí uno distinto.");

        instance.Alias = dto.Alias;
        instance.EncryptedOAuthClientSecret = dto.ClientSecret;
        instance.OAuthClientId = dto.ClientId;
        instance.OAuthConnectedAt = DateTime.Now;

        // El DbContext usa NoTracking por default (ver DbContextExtensions.AddDbContext): la
        // entidad que devolvió FirstOrDefault no está siendo vigilada, así que sin este Update
        // explícito SaveChangesAsync no detecta ningún cambio y no escribe nada.
        context.OpenProjectInstances.Update(instance);
        await context.SaveChangesAsync();
    }
}