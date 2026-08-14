using Application.Ports.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BotController(
    IAiIntentService geminiIntentService,
    IAudioTranscriptionService audioTranscriptionService,
    IConversationContextService conversationContextService,
    ILogger<BotController> logger) : ControllerBase
{
    private const long MaxAudioSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedAudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/webm", "audio/ogg", "audio/wav", "audio/x-wav", "audio/mpeg", "audio/mp3", "audio/mp4", "audio/m4a"
    };

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

    /// <summary>
    /// Transcribe un audio a texto y procesa el resultado como un prompt de chat
    /// para la sesión indicada, devolviendo tanto la transcripción como la respuesta del bot.
    /// </summary>
    /// <param name="sessionId">El identificador de la sesión de chat.</param>
    /// <param name="audio">El archivo de audio grabado por el usuario.</param>
    [HttpPost("voice/{sessionId}")]
    [RequestSizeLimit(MaxAudioSizeBytes)]
    public async Task<IActionResult> Voice(string sessionId, IFormFile? audio)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("El ID de sesión no puede estar vacío.");
        }

        if (audio == null || audio.Length == 0)
        {
            return BadRequest("Debes enviar un archivo de audio.");
        }

        if (audio.Length > MaxAudioSizeBytes)
        {
            return BadRequest("El audio supera el tamaño máximo permitido (10 MB).");
        }

        // El navegador envía el Content-Type con parámetros (ej. "audio/webm;codecs=opus");
        // solo el tipo base nos interesa para validar el formato.
        var mediaType = audio.ContentType.Split(';')[0].Trim();
        if (!AllowedAudioContentTypes.Contains(mediaType))
        {
            return BadRequest($"Tipo de audio no soportado: {audio.ContentType}");
        }

        if (!audioTranscriptionService.IsConfigured)
        {
            return Ok(new { transcript = "", response = "⚠️ La transcripción de audio no está configurada." });
        }

        logger.LogInformation("Processing voice request for session {SessionId}", sessionId);

        string transcript;
        try
        {
            await using var stream = audio.OpenReadStream();
            transcript = await audioTranscriptionService.TranscribeAsync(stream, audio.FileName, mediaType, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error transcribing audio for session {SessionId}", sessionId);
            return Ok(new { transcript = "", response = "No pude procesar el audio enviado. Intenta de nuevo o escribe tu mensaje." });
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            return Ok(new { transcript = "", response = "No pude entender el audio, ¿puedes repetirlo o escribirlo?" });
        }

        var response = await geminiIntentService.GetIntentAsync(transcript, sessionId, HttpContext.RequestAborted);

        return Ok(new { transcript, response });
    }

    [HttpGet("chats")]
    public async Task<IActionResult> ListChats()
    {
        var chats = await conversationContextService.ListAsync(HttpContext.RequestAborted);
        return Ok(chats);
    }

    [HttpGet("chats/{sessionId}")]
    public async Task<IActionResult> GetChat(string sessionId)
    {
        var context = await conversationContextService.GetOrCreateAsync(sessionId, HttpContext.RequestAborted);
        return Ok(new
        {
            sessionId = context.SessionId,
            title = context.Title,
            messages = context.History.Select(h => new { role = h.Type, content = h.Content, timestamp = h.Timestamp })
        });
    }

    [HttpDelete("chats/{sessionId}")]
    public async Task<IActionResult> DeleteChat(string sessionId)
    {
        await conversationContextService.DeleteAsync(sessionId, HttpContext.RequestAborted);
        return NoContent();
    }
}
