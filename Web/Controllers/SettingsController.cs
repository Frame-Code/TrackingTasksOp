using Application.Dto.Auth;
using Application.Ports.UseCases.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SettingsController(
    IGetUserSettingsQuery getUserSettingsQuery,
    IUpdateNotificationSettingCommand updateNotificationSettingCommand,
    IUpdateTaskPreferencesCommand updateTaskPreferencesCommand,
    IUpdateAiApiKeyCommand updateAiApiKeyCommand) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserSettingsResponse>> GetSettings(CancellationToken ct)
    {
        return await getUserSettingsQuery.Execute(ct);
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotificationSetting([FromBody] UpdateNotificationSettingRequest request, CancellationToken ct)
    {
        await updateNotificationSettingCommand.Execute(request, ct);
        return NoContent();
    }

    [HttpPut("task-preferences")]
    public async Task<IActionResult> UpdateTaskPreferences([FromBody] UpdateTaskPreferencesRequest request, CancellationToken ct)
    {
        await updateTaskPreferencesCommand.Execute(request, ct);
        return NoContent();
    }

    /// <summary>
    /// Configura (o quita, con ApiKey vacío) la API key propia del usuario para el bot de IA.
    /// Con key propia no aplica el límite diario de la key compartida del servidor.
    /// </summary>
    [HttpPut("ai-api-key")]
    public async Task<IActionResult> UpdateAiApiKey([FromBody] UpdateAiApiKeyRequest request, CancellationToken ct)
    {
        await updateAiApiKeyCommand.Execute(request, ct);
        return NoContent();
    }
}
