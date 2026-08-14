namespace Infrastructure.Adapters.Services.Bot;

/// <summary>
/// Herramientas (tool calling nativo) declaradas para Groq. "create_task" y "start_task"
/// son acciones separadas: crear una tarea NO arranca el cronómetro. El resto de las
/// acciones sigue por JSON embebido en texto, vía las instrucciones del system prompt en
/// <see cref="GroqApiClient.BuildSystemPrompt"/>.
/// </summary>
internal static class GroqTools
{
    private static readonly object CreateTaskTool = new
    {
        type = "function",
        function = new
        {
            name = "create_task",
            description = "CREA una tarea nueva en OpenProject. NO arranca el cronómetro: " +
                           "crear y empezar a trabajar son cosas distintas. Si falta info NO crítica " +
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

    private static readonly object StartTaskTool = new
    {
        type = "function",
        function = new
        {
            name = "start_task",
            description = "INICIA el seguimiento de tiempo (cronómetro) de una tarea. Úsala cuando el " +
                          "usuario quiere EMPEZAR A TRABAJAR. No crea tareas nuevas — para eso usá 'create_task'. " +
                          "Si la tarea ya existe pasá su 'workPackageId'. Si el usuario ya tiene otra tarea " +
                          "corriendo, el sistema responde con las opciones para cerrarla; mostrale esa respuesta " +
                          "tal cual y esperá su decisión.",
            parameters = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["workPackageId"] = new { type = "integer", description = "ID del work package existente en OpenProject (si ya existe)" },
                    ["projectName"] = new { type = "string", description = "Nombre del proyecto en OpenProject (nunca un ID)" },
                    ["statusName"] = new { type = "string", description = "Nombre del estado (nunca un ID)" },
                    ["name"] = new { type = "string", description = "Nombre/asunto de la tarea" },
                    ["activityId"] = new { type = "integer", description = "ID de la actividad para registrar el tiempo (opcional)" },
                    ["comment"] = new { type = "string", description = "Comentario del time entry (opcional)" }
                },
                required = new[] { "name", "projectName" }
            }
        }
    };

    public static readonly object[] All = [CreateTaskTool, StartTaskTool];
}
