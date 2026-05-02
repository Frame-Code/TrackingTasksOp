using Infrastructure.Extensions;

namespace Web.Extensions;

public static class InitializeExtensions
{
    public static async Task<WebApplication> InitializeAsync(this WebApplication app)
    {
        await app.Services.InitializeDatabaseAsync();
        app.UseCors();
        app.UseExceptionHandler();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }
}
