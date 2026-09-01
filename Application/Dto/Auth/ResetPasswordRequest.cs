namespace Application.Dto.Auth;

public record ResetPasswordRequest(string Email, string Code, string NewPassword);
