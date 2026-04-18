using Application.Ports.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BootController(
    IGeminiIntentService geminiIntentService,
    ILogger<BootController> logger) : ControllerBase
{
    public record ChatRequest(string Prompt);

    /// <summary>
    /// Procesa un prompt de usuario para una sesión de chat específica.
    /// </summary>
    /// <param name="sessionId">El identificador de la sesión de chat. Puede ser cualquier cadena única por usuario/conversación.</param>
    /// <param name="request">El objeto de solicitud con el prompt del usuario.</param>
    /// <returns>La respuesta del modelo de IA.</returns>
    [HttpPost("chat/{sessionId}")]
    public async Task<IActionResult> Chat(string sessionId, [FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("El prompt no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("El ID de sesión no puede estar vacío.");
        }
        
        logger.LogInformation("Processing chat request for session {SessionId}", sessionId);
        var response = await geminiIntentService.GetIntentAsync(request.Prompt, sessionId, HttpContext.RequestAborted);

        return Ok(new { response });
    }
}
