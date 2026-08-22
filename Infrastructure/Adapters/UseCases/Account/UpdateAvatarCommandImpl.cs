using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Application.Ports.UseCases.Account;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Adapters.UseCases.Account;

public class UpdateAvatarCommandImpl(
    TrackingTasksDbContext context,
    CurrentUser currentUser) : IUpdateAvatarCommand
{
    /// <summary>
    /// El navegador manda ~15KB. Este techo es holgado a propósito: no está para ahorrar
    /// espacio, sino para que nadie use la columna como depósito de archivos.
    /// </summary>
    private const int MaxBytes = 512 * 1024;

    public async Task Execute(UpdateAvatarRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Usuario no autenticado.");

        var existing = await context.UserAvatars.FirstOrDefaultAsync(a => a.UserId == userId, ct);

        // Vacío/null = quitar el avatar y volver a las iniciales.
        if (string.IsNullOrWhiteSpace(request.JpegBase64))
        {
            if (existing is not null)
            {
                context.UserAvatars.Remove(existing);
                await context.SaveChangesAsync(ct);
            }
            return;
        }

        var bytes = DecodeAndValidate(request.JpegBase64);

        if (existing is null)
        {
            context.UserAvatars.Add(new UserAvatar
            {
                UserId = userId,
                Jpeg = bytes,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Jpeg = bytes;
            existing.UpdatedAt = DateTime.UtcNow;
            context.UserAvatars.Update(existing);
        }

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// El front ya redimensiona y exporta JPEG, pero eso es una conveniencia del cliente, no una
    /// garantía: acá entra lo que el usuario mande. Estos bytes después se sirven a un navegador,
    /// así que se validan en la frontera.
    /// </summary>
    private static byte[] DecodeAndValidate(string base64)
    {
        // Toleramos el prefijo "data:image/jpeg;base64," por si el front lo manda entero.
        var payload = base64;
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            if (comma < 0) throw new ValidationException("La imagen no es válida.");
            payload = payload[(comma + 1)..];
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new ValidationException("La imagen no es válida.");
        }

        if (bytes.Length > MaxBytes)
            throw new ValidationException("La imagen supera el tamaño máximo permitido (512 KB).");

        // Magic bytes de JPEG: FF D8 FF.
        if (bytes.Length < 3 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
            throw new ValidationException("El archivo tiene que ser una imagen JPEG.");

        return bytes;
    }
}
