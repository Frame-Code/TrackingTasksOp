using Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Web.Extensions;

public static class InitializeExtensions
{
    public static async Task<WebApplication> InitializeAsync(this WebApplication app)
    {
        await app.Services.MigrateAsync();
        // Primero de todo: mide el request completo y publica Server-Timing (DevTools > Network > Timing).
        app.UseMiddleware<Web.Middleware.ServerTimingMiddleware>();
        app.UseCors();
        app.UseExceptionHandler();

        app.UseStatusCodePagesWithReExecute("/404.html");

        // Para rutas de API devolvemos siempre un ProblemDetails con un mensaje claro,
        // en vez de dejar que UseStatusCodePagesWithReExecute las reescriba como 404 vacío
        // (lo cual ocultaba errores reales como 401 "sesión expirada" o 500 detrás de un 404).
        // Se registra después para que, en el unwind de la respuesta, intercepte el status
        // code ANTES que el middleware anterior (más externo).
        app.UseWhen(
            ctx => ctx.Request.Path.StartsWithSegments("/api"),
            apiApp => apiApp.UseStatusCodePages(async statusCodeContext =>
            {
                var response = statusCodeContext.HttpContext.Response;
                response.ContentType = "application/problem+json";

                var (title, detail) = response.StatusCode switch
                {
                    StatusCodes.Status401Unauthorized =>
                        ("No autenticado", "Tu sesión expiró o no has iniciado sesión. Por favor, inicia sesión de nuevo."),
                    StatusCodes.Status403Forbidden =>
                        ("Acceso denegado", "No tienes permisos para realizar esta acción."),
                    StatusCodes.Status404NotFound =>
                        ("No encontrado", "El recurso solicitado no existe."),
                    _ =>
                        ("Error", "Ocurrió un error inesperado. Intenta nuevamente.")
                };

                var problem = new ProblemDetails
                {
                    Status = response.StatusCode,
                    Title = title,
                    Detail = detail
                };

                await response.WriteAsJsonAsync(problem);
            }));
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}
