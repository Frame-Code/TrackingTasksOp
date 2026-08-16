using System.Globalization;
using System.Text.Json;

namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Helpers para leer parámetros débilmente tipados (Dictionary&lt;string, object&gt;)
/// devueltos por la deserialización de las acciones JSON de Groq.
/// </summary>
public static class GroqActionParams
{
    public static int GetInt(Dictionary<string, object>? d, params string[] keys)
    {
        if (d == null) return 0;
        foreach (var k in keys)
        {
            if (d.TryGetValue(k, out var v) && v != null)
                return v is JsonElement e ? (e.ValueKind == JsonValueKind.Number ? e.GetInt32() : 0) : Convert.ToInt32(v);
        }
        return 0;
    }

    public static int? GetNullableInt(Dictionary<string, object>? d, params string[] keys)
    {
        if (d == null) return null;
        foreach (var k in keys)
        {
            if (d.TryGetValue(k, out var v) && v != null)
            {
                if (v is JsonElement e)
                {
                    return e.ValueKind == JsonValueKind.Number ? e.GetInt32() : null;
                }
                return Convert.ToInt32(v);
            }
        }
        return null;
    }

    /// <summary>
    /// Lee un número decimal (horas estimadas). El LLM a veces lo manda como número y a veces
    /// como texto ("1.5" o "1,5" según el idioma), así que se aceptan ambos y se descarta lo
    /// que no se pueda interpretar en vez de reventar la creación de la tarea.
    /// </summary>
    public static double? GetNullableDouble(Dictionary<string, object>? d, params string[] keys)
    {
        if (d == null) return null;
        foreach (var k in keys)
        {
            if (!d.TryGetValue(k, out var v) || v == null) continue;

            if (v is JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.Number) return e.GetDouble();
                if (e.ValueKind == JsonValueKind.String) return ParseInvariant(e.GetString());
                return null;
            }

            return v is string s ? ParseInvariant(s) : Convert.ToDouble(v);
        }
        return null;
    }

    private static double? ParseInvariant(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return double.TryParse(raw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Lee un booleano. Si la clave no viene (el LLM no lo mandó), devuelve <paramref name="fallback"/>.
    /// </summary>
    public static bool GetBool(Dictionary<string, object>? d, string key, bool fallback)
    {
        if (d == null) return fallback;
        if (d.TryGetValue(key, out var v) && v != null)
        {
            if (v is JsonElement e)
                return e.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(e.GetString(), out var b) ? b : fallback,
                    _ => fallback
                };
            return bool.TryParse(v.ToString(), out var parsed) ? parsed : fallback;
        }
        return fallback;
    }

    public static DateOnly? GetDate(Dictionary<string, object>? d, params string[] keys)
    {
        var raw = GetStr(d, keys);
        return DateOnly.TryParse(raw, out var date) ? date : null;
    }

    public static string GetStr(Dictionary<string, object>? d, params string[] keys)
    {
        if (d == null) return "";
        foreach (var k in keys)
        {
            if (d.TryGetValue(k, out var v) && v != null)
                return v.ToString() ?? "";
        }
        return "";
    }

    /// <summary>
    /// Lee un parámetro de tipo objeto (ej. "customFields": { "Area": "Producción", "Modulo": "Backend" })
    /// y lo devuelve como un diccionario string-string.
    /// </summary>
    public static Dictionary<string, string>? GetDict(Dictionary<string, object>? d, params string[] keys)
    {
        if (d == null) return null;
        foreach (var k in keys)
        {
            if (d.TryGetValue(k, out var v) && v is JsonElement { ValueKind: JsonValueKind.Object } e)
            {
                var result = new Dictionary<string, string>();
                foreach (var prop in e.EnumerateObject())
                    result[prop.Name] = prop.Value.ToString();
                return result;
            }
        }
        return null;
    }
}
