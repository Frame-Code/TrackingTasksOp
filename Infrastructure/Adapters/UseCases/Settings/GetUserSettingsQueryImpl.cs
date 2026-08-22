using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Settings;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.UseCases.Settings;

public class GetUserSettingsQueryImpl(
    TrackingTasksDbContext context,
    UserManager<ApplicationUser> userManager,
    CurrentUser currentUser) : IGetUserSettingsQuery
{
    public async Task<UserSettingsResponse> Execute(CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var appUser = await userManager.FindByIdAsync(userId)
            ?? throw new ApplicationException($"User {userId} not found while loading settings");

        // AnyAsync y no cargar la entidad: acá solo hace falta saber si existe, y esta consulta
        // corre en cada carga de página.
        var hasAvatar = await context.UserAvatars.AnyAsync(a => a.UserId == userId, ct);

        var savedSettings = await context.Set<UserNotificationSetting>()
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.TypeCode, ct);

        var notifications = NotificationTypeCodes.All
            .Select(typeCode => savedSettings.TryGetValue(typeCode, out var saved)
                ? new NotificationSettingDto(typeCode, saved.Enabled, saved.IntervalMinutes)
                : new NotificationSettingDto(typeCode, true, NotificationTypeCodes.DefaultIntervalMinutes))
            .ToList();

        return new UserSettingsResponse
        {
            Notifications = notifications,
            OpenProjectInstanceUrl = currentUser.OpenProjectInstanceUrl,
            Email = appUser.Email!,
            PauseDefaultBehavior = appUser.PauseDefaultBehavior.ToString(),
            SkipCancelConfirmation = appUser.SkipCancelConfirmation,
            AddRandomSlackTime = appUser.AddRandomSlackTime,
            DefaultStatusIds = ParseStatusIds(appUser.DefaultStatusFilterIds),
            HasCustomAiApiKey = !string.IsNullOrEmpty(appUser.EncryptedGroqApiKey),
            TwoFactorEnabled = appUser.TwoFactorEnabled,
            HasAvatar = hasAvatar
        };
    }

    private static List<int> ParseStatusIds(string? raw) =>
        string.IsNullOrEmpty(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();
}
