using Application.Dto.Auth;

namespace Application.Ports.UseCases.Account;

public interface IGetAvatarQuery
{
    /// <summary>
    /// Avatar del usuario autenticado, o null si nunca subió uno (la UI cae a las iniciales).
    /// </summary>
    Task<AvatarResponse?> Execute(CancellationToken ct = default);
}
