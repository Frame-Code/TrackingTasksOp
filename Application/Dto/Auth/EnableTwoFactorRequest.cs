namespace Application.Dto.Auth;

/// <summary>Código de 6 dígitos que confirma que la app de autenticación quedó sincronizada.</summary>
public record EnableTwoFactorRequest(string Code);
