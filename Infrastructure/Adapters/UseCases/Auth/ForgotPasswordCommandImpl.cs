using System.Security.Cryptography;
using System.Text;
using Application.Dto.Auth;
using Application.Ports.Services;
using Application.Ports.UseCases.Auth;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Adapters.UseCases.Auth;

public class ForgotPasswordCommandImpl(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender) : IForgotPasswordCommand
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    public async Task ExecuteAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Mismo resultado exista o no el usuario: no filtrar qué correos están registrados.
        if (user is null) return;

        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        user.PasswordResetCodeHash = Hash(code);
        user.PasswordResetCodeExpiresAt = DateTime.UtcNow.Add(CodeLifetime);
        await userManager.UpdateAsync(user);

        var html = $"""
            <p>Usá este código para recuperar tu contraseña en TrackingTasksOp:</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px;">{code}</p>
            <p>Vence en 15 minutos. Si no fuiste vos, ignorá este correo.</p>
            """;

        await emailSender.SendAsync(user.Email!, "Código de recuperación - TrackingTasksOp", html, ct);
    }

    internal static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
