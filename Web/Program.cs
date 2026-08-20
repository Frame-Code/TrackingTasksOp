using System.Text.Json.Serialization;
using Web.Extensions;
using Web.Middleware;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc.Authorization;
using Serilog;

// Ruta absoluta, anclada a la carpeta del .exe: como servicio de Windows, el directorio
// de trabajo del proceso es C:\Windows\System32 por defecto, no la carpeta de la app —
// una ruta relativa como "logs/..." termina escribiendo ahí en vez de junto al .exe.
var logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "trackingtasksop-.log");

// Logger "bootstrap": queda activo desde el primer instante, antes de armar el host.
// Necesario porque errores como la conexión a Redis en AddServices() pasan ANTES de
// que exista un ILogger normal, y antes solo quedaban en el Visor de Eventos de Windows.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando TrackingTasksOp");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day));

    if (OperatingSystem.IsWindows())
        builder.Host.UseWindowsService();
    else
        builder.Host.UseSystemd();

    builder.Services.AddControllers(opt => opt.Filters.Add(new AuthorizeFilter()))
        .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddSwaggerGen();
    builder.Services.AddTrackingDataProtection(builder.Configuration, builder.Environment.ContentRootPath);
    builder.Services.AddIdentityAndAuth(builder.Configuration);
    builder.Services.AddHttpClients(builder.Configuration, builder.Environment.IsDevelopment());
    builder.Services.AddServices(builder.Configuration);
    builder.Services.AddDbContext(builder.Configuration);
    builder.Services.ConfigureCors(builder.Configuration);

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    await app.InitializeAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TrackingTasksOp terminó inesperadamente durante el arranque");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
