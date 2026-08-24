using Application.Dto.OpInstance;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.Repositories;
using Infrastructure.DataAccess;
using Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Repositories;

public class OpInstanceRepositoryImplTests
{
    private static TrackingTasksDbContext BuildContext()
    {
        // NoTracking por default: replica DbContextExtensions.AddDbContext (la config real),
        // para que un Save() que olvide un Update() explícito falle acá y no solo en producción.
        var options = new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        return new TrackingTasksDbContext(options);
    }

    private static OpInstanceRepositoryImpl BuildRepository(TrackingTasksDbContext context) =>
        new(NullLogger<OpInstanceRepositoryImpl>.Instance, context);

    [Fact]
    public async Task Save_ExistingInstance_UpdatesAliasAndOAuthFields()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" });
        await context.SaveChangesAsync();
        // Simula un DbContext scoped fresco por request: sin esto, la entidad recién sembrada
        // sigue trackeada y choca con la instancia sin trackear que devuelve FirstOrDefault
        // bajo NoTracking, tirando "another instance with the same key is already being tracked".
        context.ChangeTracker.Clear();

        var repository = BuildRepository(context);
        var dto = new SaveOpInstanceDto(1, "Mi Organización", "client-id-1", "encrypted-secret");

        await repository.Save(dto);

        var updated = await context.OpenProjectInstances.AsNoTracking().FirstAsync(x => x.Id == 1);
        Assert.Equal("Mi Organización", updated.Alias);
        Assert.Equal("client-id-1", updated.OAuthClientId);
        Assert.Equal("encrypted-secret", updated.EncryptedOAuthClientSecret);
        Assert.NotNull(updated.OAuthConnectedAt);
    }

    [Fact]
    public async Task Save_AliasAlreadyUsedByAnotherInstance_ThrowsDuplicateAliasException()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 1, BaseUrl = "http://op-a.example.com", Alias = "Acme" });
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 2, BaseUrl = "http://op-b.example.com" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = BuildRepository(context);
        var dto = new SaveOpInstanceDto(2, "acme", "client-id", "secret"); // mismo alias, distinta capitalización

        await Assert.ThrowsAsync<DuplicateAliasException>(() => repository.Save(dto));
    }

    [Fact]
    public async Task Save_SameAliasOnSameInstance_DoesNotThrow()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com", Alias = "Acme" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = BuildRepository(context);
        var dto = new SaveOpInstanceDto(1, "Acme", "client-id", "secret"); // reconecta la misma instancia

        await repository.Save(dto);

        var updated = await context.OpenProjectInstances.AsNoTracking().FirstAsync(x => x.Id == 1);
        Assert.Equal("client-id", updated.OAuthClientId);
    }

    [Fact]
    public async Task Save_InstanceDoesNotExist_ThrowsOpInstanceNotFoundException()
    {
        await using var context = BuildContext();
        var repository = BuildRepository(context);
        var dto = new SaveOpInstanceDto(999, "Alias", "client-id", "secret");

        await Assert.ThrowsAsync<OpInstanceNotFoundException>(() => repository.Save(dto));
    }

    [Fact]
    public async Task Lists_ReturnsIsOAuthConnected_OnlyWhenClientIdIsSet()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 1, BaseUrl = "http://connected.example.com", OAuthClientId = "client-1" });
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 2, BaseUrl = "http://not-connected.example.com" });
        await context.SaveChangesAsync();

        var repository = BuildRepository(context);

        var result = (await repository.Lists()).ToList();

        Assert.Equal(2, result.Count);
        Assert.True(result.Single(x => x.Id == 1).IsOAuthConnected);
        Assert.False(result.Single(x => x.Id == 2).IsOAuthConnected);
    }

    [Fact]
    public async Task Lists_MissingAlias_DefaultsToDash()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance { Id = 1, BaseUrl = "http://op.example.com" });
        await context.SaveChangesAsync();

        var repository = BuildRepository(context);

        var result = (await repository.Lists()).Single();

        Assert.Equal("-", result.Alias);
    }

    [Fact]
    public async Task GetOpInstance_ExistingInstance_ReturnsBaseUrlAndCredentials()
    {
        await using var context = BuildContext();
        context.OpenProjectInstances.Add(new OpenProjectInstance
        {
            Id = 1,
            BaseUrl = "http://op.example.com",
            OAuthClientId = "client-id",
            EncryptedOAuthClientSecret = "encrypted-secret"
        });
        await context.SaveChangesAsync();

        var repository = BuildRepository(context);

        var result = await repository.GetOpInstance(1);

        Assert.NotNull(result);
        Assert.Equal("http://op.example.com", result!.BaseUrl);
        Assert.Equal("client-id", result.ClientId);
        Assert.Equal("encrypted-secret", result.ClientSecret);
    }

    [Fact]
    public async Task GetOpInstance_InstanceDoesNotExist_ReturnsNull()
    {
        await using var context = BuildContext();
        var repository = BuildRepository(context);

        var result = await repository.GetOpInstance(999);

        Assert.Null(result);
    }
}
