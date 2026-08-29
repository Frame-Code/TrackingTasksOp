namespace Application.Ports.UseCases.Tasks;

/// <summary>
/// Registra que la sesión abierta del usuario sigue viva. Es la evidencia con la que se cierra
/// una sesión que el usuario nunca cerró, en vez de inventarle un final.
/// </summary>
public interface IRecordSessionHeartbeatCommand
{
    /// <returns>true si había una sesión abierta que actualizar.</returns>
    Task<bool> Execute(CancellationToken ct = default);
}
