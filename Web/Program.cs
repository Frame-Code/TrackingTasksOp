using System.Text.Json.Serialization;
using Web.Extensions;
using Web.Middleware;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);


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
builder.Services.AddTrackingDataProtection(builder.Configuration);
builder.Services.AddIdentityAndAuth(builder.Configuration);
builder.Services.AddHttpClients(builder.Configuration);
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
