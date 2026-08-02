namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Herramientas (tool calling nativo) declaradas para Groq. Piloto: solo "start_task",
/// la acción que más fallaba con el mecanismo anterior (JSON embebido en texto libre,
/// parseado con regex). El resto de las acciones sigue con ese mecanismo por ahora,
/// vía las instrucciones del system prompt en <see cref="GroqApiClient.BuildSystemPrompt"/>.
/// </summary>
internal static class GroqTools
{
    private static readonly object StartTaskTool = new
    {
        type = "function",
        function = new
        {
            name = "start_task",
            description = "Inicia o crea una tarea nueva en OpenProject. Si falta info NO crítica " +
                           "con default razonable (fechas, descripción, asignado), confirmá primero " +
                           "con el usuario en texto antes de llamar esta función. Si el sistema responde " +
                           "pidiendo campos adicionales, volvé a llamarla agregando esos valores a " +
                           "'customFields', sin repetir los demás parámetros si ya los diste antes.",
            parameters = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["projectName"] = new { type = "string", description = "Nombre del proyecto en OpenProject (nunca un ID)" },
                    ["statusName"] = new { type = "string", description = "Nombre del estado inicial (nunca un ID)" },
                    ["name"] = new { type = "string", description = "Nombre/asunto de la tarea" },
                    ["description"] = new { type = "string" },
                    ["assigneeName"] = new { type = "string", description = "Nombre del asignado (nunca un ID), o 'yo' si el usuario pide asignarse a sí mismo" },
                    ["responsibleName"] = new { type = "string", description = "Nombre del responsable (nunca un ID), o 'yo' si el usuario pide asignarse a sí mismo" },
                    ["startDate"] = new { type = "string", description = "Fecha de inicio en formato yyyy-MM-dd" },
                    ["dueDate"] = new { type = "string", description = "Fecha de fin en formato yyyy-MM-dd" },
                    ["customFields"] = new
                    {
                        type = "object",
                        additionalProperties = new { type = "string" },
                        description = "Campos personalizados que haya pedido el sistema (nombre -> valor), ej. {\"Area\": \"Producción\"}"
                    }
                },
                required = new[] { "name", "projectName" }
            }
        }
    };

    public static readonly object[] All = [StartTaskTool];
}
