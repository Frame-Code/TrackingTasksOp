using Application.Ports.Auth;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.Adapters.UseCases.Tasks;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Tasks;

public class CancelTaskSessionCommandImplTests
{
    private class FakeCurrentUser(string? userId = "user-1") : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => "http://op.example.com";
        public override int? OpenProjectInstanceId => 2;
        public override int? OpenProjectUserId => 7;
    }

    private static TrackingTasksDbContext BuildDbContext() =>
        new(new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static CancelTaskSessionCommandImpl BuildUseCase(TrackingTasksDbContext context, string userId = "user-1") =>
        new(context, new FakeCurrentUser(userId), new Mock<ILogger<CancelTaskSessionCommandImpl>>().Object);

    [Fact]
    public async Task Execute_NoActiveSession_ReturnsFalse()
    {
        using var context = BuildDbContext();
        var useCase = BuildUseCase(context);

        var result = await useCase.Execute(99);

        Assert.False(result);
    }

    [Fact]
    public async Task Execute_ActiveSessionExists_RemovesDetailAndReturnsTrue()
    {
        using var context = BuildDbContext();
        context.TasksTimeDetails.Add(new TaskTimeDetail
        {
            Id      = 1,
            IdTask  = 10,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime = null,
            UserId  = "user-1"
        });
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context);
        var result  = await useCase.Execute(10);

        Assert.True(result);
        Assert.Empty(await context.TasksTimeDetails.ToListAsync());
    }

    [Fact]
    public async Task Execute_OnlyRemovesOpenDetail_LeavesClosedDetailIntact()
    {
        using var context = BuildDbContext();
        context.TasksTimeDetails.AddRange(
            new TaskTimeDetail
            {
                Id        = 1,
                IdTask    = 10,
                StartTime = DateTime.Now.AddHours(-3),
                EndTime   = DateTime.Now.AddHours(-2),   // ya cerrado
                UserId    = "user-1"
            },
            new TaskTimeDetail
            {
                Id        = 2,
                IdTask    = 10,
                StartTime = DateTime.Now.AddHours(-1),
                EndTime   = null,                         // sesión activa
                UserId    = "user-1"
            });
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context);
        var result  = await useCase.Execute(10);

        Assert.True(result);
        var remaining = await context.TasksTimeDetails.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(1, remaining[0].Id);
        Assert.NotNull(remaining[0].EndTime);
    }

    [Fact]
    public async Task Execute_WrongWorkPackageId_ReturnsFalse()
    {
        using var context = BuildDbContext();
        context.TasksTimeDetails.Add(new TaskTimeDetail
        {
            Id        = 1,
            IdTask    = 10,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime   = null,
            UserId    = "user-1"
        });
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context);
        var result  = await useCase.Execute(99);   // ID distinto

        Assert.False(result);
        Assert.Single(await context.TasksTimeDetails.ToListAsync());
    }

    [Fact]
    public async Task Execute_SesionActivaEsDeOtroUsuario_NoLaCancelaYDevuelveFalse()
    {
        // Dos tenants distintos pueden trackear el mismo WorkPackageId numérico. Sin el
        // filtro por UserId, "cancelar" borraba la sesión activa de OTRO usuario.
        using var context = BuildDbContext();
        context.TasksTimeDetails.Add(new TaskTimeDetail
        {
            Id        = 1,
            IdTask    = 10,
            StartTime = DateTime.Now.AddHours(-1),
            EndTime   = null,
            UserId    = "user-2"   // el dueño real de la sesión
        });
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context, userId: "user-1"); // atacante/otro tenant
        var result  = await useCase.Execute(10);

        Assert.False(result);
        Assert.Single(await context.TasksTimeDetails.ToListAsync());
        Assert.Null((await context.TasksTimeDetails.FirstAsync()).EndTime);
    }
}
