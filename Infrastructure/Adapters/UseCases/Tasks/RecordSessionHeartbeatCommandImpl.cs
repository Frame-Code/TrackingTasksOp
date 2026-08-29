using Application.Ports.Auth;
using Application.Ports.UseCases.Tasks;
using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.UseCases.Tasks;

public class RecordSessionHeartbeatCommandImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser) : IRecordSessionHeartbeatCommand
{
    public async Task<bool> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        // El sello lo pone el servidor y no el cliente: si viniera en el body, un reloj
        // adelantado (o un cliente manipulado) podría estirar la sesión a voluntad.
        //
        // ExecuteUpdate en vez de leer-modificar-guardar porque esto corre cada minuto por cada
        // usuario con sesión abierta: es un solo UPDATE, sin materializar la entidad ni pasar
        // por el change tracker.
        var updated = await context.Set<TaskTimeDetail>()
            .Where(d => d.UserId == userId && d.EndTime == null)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastHeartbeat, DateTime.Now), ct);

        return updated > 0;
    }
}
