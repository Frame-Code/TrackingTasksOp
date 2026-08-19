using System.Diagnostics;

namespace Infrastructure.Adapters.Http;

/// <summary>
/// Recolecta tiempos de las operaciones lentas de un request para publicarlos en la
/// cabecera <c>Server-Timing</c>, que el navegador muestra en DevTools → Network →
/// Timing. Así los números quedan donde se está diagnosticando el problema, en vez de
/// en la consola del servidor.
/// </summary>
public class RequestTimings
{
    private readonly List<(string Name, long Ms)> _marks = [];
    private readonly object _gate = new();

    /// <summary>Mide una operación async y la registra. Devuelve su resultado tal cual.</summary>
    public async Task<T> MeasureAsync<T>(string name, Func<Task<T>> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return await operation();
        }
        finally
        {
            Add(name, stopwatch.ElapsedMilliseconds);
        }
    }

    // Las páginas se piden en paralelo, así que varios hilos escriben aquí.
    public void Add(string name, long milliseconds)
    {
        lock (_gate) _marks.Add((name, milliseconds));
    }

    private readonly List<string> _notes = [];

    /// <summary>
    /// Nota de diagnóstico para la cabecera <c>X-Diagnostics</c>. Sirve para ver desde
    /// DevTools por qué se degradó una optimización, sin tener que entrar a los logs
    /// del servidor.
    /// </summary>
    public void Note(string message)
    {
        lock (_gate) _notes.Add(message);
    }

    public string? ToDiagnosticsHeader()
    {
        lock (_gate)
        {
            if (_notes.Count == 0) return null;

            // Las cabeceras no admiten saltos de línea y conviene acotar el largo.
            var joined = string.Join(" | ", _notes).ReplaceLineEndings(" ");
            return joined.Length > 900 ? joined[..900] : joined;
        }
    }

    /// <summary>Valor de la cabecera, o null si no se midió nada en este request.</summary>
    public string? ToHeaderValue()
    {
        lock (_gate)
        {
            if (_marks.Count == 0) return null;

            // Se numeran porque Server-Timing exige nombres únicos dentro de la cabecera.
            return string.Join(", ", _marks.Select((m, i) =>
                $"{Sanitize(m.Name)}-{i};dur={m.Ms}"));
        }
    }

    private static string Sanitize(string name) =>
        new(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
