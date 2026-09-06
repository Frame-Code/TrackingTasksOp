using System.Text.Json;

namespace Infrastructure.Adapters.UseCases.WorkPackages;

internal static class OpenProjectError
{
    /// <summary>
    /// El "message" del error de OpenProject, que ya viene redactado para una persona
    /// ("El padre no es válido porque..."). El bot y la UI muestran ese texto: el JSON crudo
    /// no le dice nada a nadie. Si no se puede parsear se devuelve tal cual vino.
    /// </summary>
    public static string ExtractMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? json : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
