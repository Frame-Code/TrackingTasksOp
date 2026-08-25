using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Xunit;
// El dominio define su propia entidad Task, que choca con System.Threading.Tasks.Task.
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.DataAccess;

/// <summary>
/// Npgsql valida el DateTimeKind contra el tipo de la columna en AMBAS direcciones, y la app
/// mezcla Local (StartTime/EndTime) con Utc (tokens OAuth, auditoría). Migrando de SQL Server
/// eso pasó desapercibido hasta el primer registro real, porque crear el esquema no ejecuta
/// ninguna escritura.
///
/// Estos tests escriben de verdad. Si alguien saca el UnspecifiedDateTimeConverter o cambia el
/// tipo de columna, fallan acá y no en producción.
/// </summary>
public class DateTimeKindPersistenceTests
{
    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION_STRING");

    private static TrackingTasksDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TrackingTasksDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    private static async Task GuardarYBorrar(DateTime createdAt)
    {
        await using var db = NewContext();
        var instance = new OpenProjectInstance
        {
            BaseUrl = $"http://test-{Guid.NewGuid():N}:8080",
            CreatedAt = createdAt
        };

        db.OpenProjectInstances.Add(instance);
        await db.SaveChangesAsync();

        db.OpenProjectInstances.Remove(instance);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Guardar_fecha_en_Utc_no_falla()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;
        // Kind=Utc: exactamente el caso que rompía el registro de usuarios.
        await GuardarYBorrar(DateTime.UtcNow);
    }

    [Fact]
    public async Task Guardar_fecha_en_hora_local_no_falla()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;
        // Kind=Local: el caso del "reloj de pared" que usan StartTime/EndTime.
        await GuardarYBorrar(DateTime.Now);
    }

    /// <summary>El valor no se altera al normalizar el Kind: solo se descarta la etiqueta.</summary>
    [Fact]
    public async Task El_valor_guardado_conserva_el_instante()
    {
        if (string.IsNullOrEmpty(ConnectionString)) return;

        var esperado = new DateTime(2026, 8, 24, 20, 55, 16, DateTimeKind.Utc);

        await using var db = NewContext();
        var instance = new OpenProjectInstance
        {
            BaseUrl = $"http://test-{Guid.NewGuid():N}:8080",
            CreatedAt = esperado
        };

        db.OpenProjectInstances.Add(instance);
        await db.SaveChangesAsync();

        var leido = await db.OpenProjectInstances
            .AsNoTracking()
            .FirstAsync(i => i.Id == instance.Id);

        Assert.Equal(esperado.ToString("yyyy-MM-dd HH:mm:ss"),
                     leido.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

        db.OpenProjectInstances.Remove(instance);
        await db.SaveChangesAsync();
    }
}
