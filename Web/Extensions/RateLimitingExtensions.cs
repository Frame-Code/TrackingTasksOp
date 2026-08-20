using System.Security.Claims;
using System.Threading.RateLimiting;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Web.Extensions;

public static class RateLimitingExtensions
{
    public static void AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimitSettings").Get<RateLimitSettings>()
            ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas solicitudes",
                    Detail = "Estás haciendo demasiadas solicitudes. Esperá un momento e intentá de nuevo."
                }, ct));
            };

            // Límite general de toda la API: por usuario autenticado (mismo claim que usa
            // HttpContextCurrentUser), o por IP para requests sin sesión.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var key = httpContext.User.Identity?.IsAuthenticated == true
                    ? $"user:{httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)}"
                    : $"ip:{httpContext.Connection.RemoteIpAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0
                });
            });

            // Política aparte, más estricta, para login/registro: ahí todavía no hay sesión
            // (el límite global por usuario no aplica) y es el blanco típico de fuerza bruta/spam.
            options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.AuthPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.AuthWindowSeconds),
                    QueueLimit = 0
                }));
        });
    }
}
