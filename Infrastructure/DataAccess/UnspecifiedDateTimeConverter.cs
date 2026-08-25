using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.DataAccess;

/// <summary>
/// Borra el <see cref="DateTimeKind"/> de toda fecha que entra o sale de la base.
///
/// Npgsql valida el Kind contra el tipo de la columna y es estricto en AMBAS direcciones:
/// <c>timestamptz</c> exige Kind=Utc, y <c>timestamp without time zone</c> RECHAZA Kind=Utc.
/// La app mezcla los dos — StartTime/EndTime usan DateTime.Now (Local) porque significan
/// "reloj de pared", mientras que los tokens OAuth y los logs de auditoría usan UtcNow —
/// así que con cualquier tipo de columna, la mitad de las escrituras fallaba.
///
/// Pasando todo a Unspecified el valor numérico no se toca: solo se descarta la etiqueta que
/// Npgsql valida. Las comparaciones siguen funcionando porque comparan ticks, no Kind
/// (ej. OpenProjectAuthHeaderProvider contrasta OAuthTokenExpiresAt contra DateTime.UtcNow,
/// y ambos lados siguen siendo el mismo instante en UTC).
/// </summary>
public class UnspecifiedDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UnspecifiedDateTimeConverter()
        : base(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified))
    {
    }
}
