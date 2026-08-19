using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Settings;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.UseCases.Settings;

public class UpdateNotificationSettingCommandImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser) : IUpdateNotificationSettingCommand
{
    public async Task Execute(UpdateNotificationSettingRequest request, CancellationToken ct = default)
    {
        if (!NotificationTypeCodes.All.Contains(request.TypeCode))
            throw new ValidationException($"Tipo de notificación desconocido: '{request.TypeCode}'.");

        if (request.IntervalMinutes is <= 0 or > 1440)
            throw new ValidationException("El intervalo debe estar entre 1 y 1440 minutos.");

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var existing = await context.Set<UserNotificationSetting>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.TypeCode == request.TypeCode, ct);

        if (existing is null)
        {
            context.Set<UserNotificationSetting>().Add(new UserNotificationSetting
            {
                UserId = userId,
                TypeCode = request.TypeCode,
                Enabled = request.Enabled,
                IntervalMinutes = request.IntervalMinutes
            });
        }
        else
        {
            existing.Enabled = request.Enabled;
            existing.IntervalMinutes = request.IntervalMinutes;
            // DbContext usa NoTracking global (ver DbContextExtensions.cs): sin esto,
            // SaveChangesAsync no detecta el cambio y no emite el UPDATE.
            context.Entry(existing).State = EntityState.Modified;
        }

        await context.SaveChangesAsync(ct);
    }
}
