using Web.Infrastructure.Config.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClients(builder.Configuration);
builder.Services.AddServices(builder.Configuration);
builder.Services.AddDbContext(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.ConfigurateDbAsync();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
