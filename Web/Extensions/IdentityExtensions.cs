using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;

namespace Web.Extensions;

public static class IdentityExtensions
{
    public static void AddIdentityAndAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetRequiredSection("IdentitySettings").Get<IdentitySettings>()
            ?? throw new Exception("No se pudo bindear IdentitySettings");

        var cookieSettings = configuration.GetSection("CookieSettings").Get<CookieSettings>()
            ?? throw new Exception("No se pudo bindear CookieSettings");

        services.AddIdentity<ApplicationUser, IdentityRole>(opt => {
            opt.Password.RequireDigit = settings.Password.RequireDigit;
            opt.Password.RequiredLength = settings.Password.RequiredLength;
            opt.Password.RequireUppercase = settings.Password.RequireUppercase;
            opt.Password.RequireNonAlphanumeric = settings.Password.RequireNonAlphanumeric;

            opt.Lockout.DefaultLockoutTimeSpan = settings.Lockout.DefaultLockoutTimeSpan;
            opt.Lockout.MaxFailedAccessAttempts = settings.Lockout.MaxFailedAccessAttempts;

            opt.User.RequireUniqueEmail = settings.User.RequireUniqueEmail;
        })
        .AddEntityFrameworkStores<TrackingTasksDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(opt =>
        {
            //Identidad de la cookie
            opt.Cookie.Name = "TrackingTaskOp.Auth";
            
            //Seguridad de la cookie de autentication
            opt.Cookie.HttpOnly = true;
            opt.Cookie.SecurePolicy = cookieSettings.UseSecurePolicy ? CookieSecurePolicy.Always : CookieSecurePolicy.None;
            opt.Cookie.SameSite = SameSiteMode.Lax;
        
            //Lifetime
            opt.ExpireTimeSpan = TimeSpan.FromDays(cookieSettings.LifeTime);
            opt.SlidingExpiration = true;
    
            opt.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            
            opt.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }
}
