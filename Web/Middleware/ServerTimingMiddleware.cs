using System.Diagnostics;
using Infrastructure.Adapters.Http;

namespace Web.Middleware;

/// <summary>
/// Publica los tiempos recolectados en <see cref="RequestTimings"/> como cabecera
/// <c>Server-Timing</c>, visible en DevTools → Network → Timing sin herramientas extra.
/// </summary>
public class ServerTimingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestTimings timings)
    {
        var stopwatch = Stopwatch.StartNew();

        // Las cabeceras hay que escribirlas antes de que empiece a salir el cuerpo.
        context.Response.OnStarting(() =>
        {
            timings.Add("total", stopwatch.ElapsedMilliseconds);

            var value = timings.ToHeaderValue();
            if (value is not null)
            {
                context.Response.Headers["Server-Timing"] = value;
                // Sin esto el navegador no deja leer la cabecera desde JavaScript.
                context.Response.Headers["Timing-Allow-Origin"] = "*";
            }

            var diagnostics = timings.ToDiagnosticsHeader();
            if (diagnostics is not null)
                context.Response.Headers["X-Diagnostics"] = diagnostics;

            return Task.CompletedTask;
        });

        await next(context);
    }
}
