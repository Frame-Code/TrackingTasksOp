namespace Application.Dto.Auth;

/// <summary>
/// Códigos de un solo uso, válidos en lugar del código de la app.
///
/// Se emiten al activar el 2FA y se pueden volver a generar cuando el usuario quiera: generar
/// una tanda nueva invalida la anterior. Por eso no son un secreto que "se pierde para siempre"
/// — son reemplazables, igual que en GitHub o Google.
/// </summary>
public record RecoveryCodesResponse(IReadOnlyList<string> RecoveryCodes);
