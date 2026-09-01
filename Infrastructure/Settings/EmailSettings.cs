namespace Infrastructure.Settings;

public class EmailSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = null!;

    /// <summary>Contraseña de aplicación de Gmail (16 caracteres), no la contraseña normal de la cuenta.</summary>
    public string Password { get; set; } = null!;
    public string From { get; set; } = null!;
    public string FromName { get; set; } = null!;
}
