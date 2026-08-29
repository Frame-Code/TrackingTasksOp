using Domain.Entities.TrackingTasksEntities;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.DataAccess;

public class TrackingTasksDbContext(DbContextOptions<TrackingTasksDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    /// <summary>
    /// El key ring de Data Protection, que descifra <c>LocalCredentials.EncryptedApiKey</c>.
    /// Vive en la misma base que los datos que protege a propósito: cuando estaba en el disco
    /// (volumen <c>keysdata</c>) eran DOS artefactos que había que respaldar juntos, y un
    /// backup que capturaba solo la base dejaba las API keys de OpenProject indescifrables.
    /// Ahora un único <c>pg_dump</c> se lleva las dos mitades y no se pueden desincronizar.
    /// El ring va cifrado con certificado (ver <c>DataProtectionExtensions</c>) para que ese
    /// mismo dump no exponga las API keys si el archivo se filtra.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<Task> Tasks { get; set; } = null!;
    public DbSet<TaskTimeDetail> TasksTimeDetails { get; set; } = null!;
    public DbSet<StatusTask> StatusTasks { get; set; } = null!;
    public DbSet<MigrationData> MigrationsData { get; set; } = null!;
    public DbSet<OpenProjectInstance> OpenProjectInstances { get; set; } = null!;
    public DbSet<UserCredential> UserCredentials { get; set; } = null!;
    public DbSet<LocalCredential> LocalCredentials { get; set; } = null!;
    public DbSet<OAuthCredential> OAuthCredentials { get; set; } = null!;
    public DbSet<AuthAuditLog> AuthAuditLogs { get; set; } = null!;
    public DbSet<UserNotificationSetting> UserNotificationSettings { get; set; } = null!;
    public DbSet<UserAvatar> UserAvatars { get; set; } = null!;

    /// <summary>
    /// Todas las fechas se guardan como <c>timestamp without time zone</c>, con el
    /// <see cref="DateTimeKind"/> normalizado a Unspecified por
    /// <see cref="UnspecifiedDateTimeConverter"/>.
    ///
    /// Las dos cosas son necesarias juntas. Npgsql valida el Kind contra el tipo de columna en
    /// AMBAS direcciones, y la app mezcla los dos: StartTime/EndTime usan DateTime.Now (Local)
    /// porque significan "reloj de pared del usuario" — igual que hacía el <c>datetime</c> de
    /// SQL Server —, mientras que los tokens OAuth y los logs de auditoría usan UtcNow.
    /// Solo con el tipo de columna, las escrituras UTC fallaban; solo con el converter, Npgsql
    /// seguiría eligiendo timestamptz por defecto.
    ///
    /// Va como convención y no columna por columna para que ninguna fecha nueva se olvide.
    /// Si algún día la app sirve a usuarios en zonas horarias distintas, esto hay que revisarlo:
    /// ahí sí corresponde UTC, y toca también la visualización y la regla de "hoy".
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UnspecifiedDateTimeConverter>()
            .HaveColumnType("timestamp without time zone");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingTasksDbContext).Assembly);
    }
}
