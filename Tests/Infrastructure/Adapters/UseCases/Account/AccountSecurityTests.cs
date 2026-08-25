using System.ComponentModel.DataAnnotations;
using Application.Dto.Auth;
using Application.Ports.Auth;
using Infrastructure.Adapters.UseCases.Account;
using Infrastructure.DataAccess;
using Infrastructure.DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.Account;

/// <summary>
/// Los cuatro rechazos que sostienen la seguridad de la pantalla de cuenta. No prueban el camino
/// feliz a propósito: lo que rompe una cuenta es que uno de estos deje de tirar, no que el
/// correcto deje de pasar (eso se nota al primer uso).
/// </summary>
public class AccountSecurityTests
{
    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
#pragma warning disable CS8625
        var mock = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object,
            null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        // El código lee Options.Tokens.AuthenticatorTokenProvider; sin esto queda null.
        mock.Object.Options = new IdentityOptions();
        return mock;
    }

    private static Mock<SignInManager<ApplicationUser>> BuildSignInManagerMock(
        Mock<UserManager<ApplicationUser>> userManagerMock)
    {
#pragma warning disable CS8625
        return new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null, null, null, null);
#pragma warning restore CS8625
    }

    private class FakeCurrentUser(string? userId) : CurrentUser
    {
        public override string? UserId => userId;
        public override bool IsAuthenticated => userId != null;
        public override string? OpenProjectInstanceUrl => null;
        public override int? OpenProjectInstanceId => null;
        public override int? OpenProjectUserId => null;
    }

    private static ApplicationUser BuildUser(bool twoFactorEnabled) =>
        new() { Id = "user-1", UserName = "test@test.com", TwoFactorEnabled = twoFactorEnabled };

    /// <summary>Devuelve el mock ya cableado a FindByIdAsync con este usuario.</summary>
    private static Mock<UserManager<ApplicationUser>> UserManagerFor(ApplicationUser user)
    {
        var mock = BuildUserManagerMock();
        mock.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        return mock;
    }

    [Fact]
    public async Task Activar_2FA_con_codigo_incorrecto_falla()
    {
        var user = BuildUser(twoFactorEnabled: false);
        var userManager = UserManagerFor(user);
        userManager
            .Setup(m => m.VerifyTwoFactorTokenAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var command = new EnableTwoFactorCommandImpl(userManager.Object, new FakeCurrentUser(user.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new EnableTwoFactorRequest("000000")));

        // Lo que realmente importa: no quedó activado con un código que nunca validó.
        userManager.Verify(m => m.SetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>(), true), Times.Never);
    }

    [Fact]
    public async Task Cambiar_contrasena_con_la_actual_incorrecta_falla()
    {
        var user = BuildUser(twoFactorEnabled: true);
        var userManager = UserManagerFor(user);
        // El segundo factor sí es válido: lo que se prueba es que la contraseña actual no se saltea.
        userManager
            .Setup(m => m.VerifyTwoFactorTokenAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        userManager
            .Setup(m => m.ChangePasswordAsync(user, "mala", "NuevaPass123!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

        var signInManager = BuildSignInManagerMock(userManager);
        var command = new ChangePasswordCommandImpl(
            userManager.Object, signInManager.Object, new FakeCurrentUser(user.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new ChangePasswordRequest("mala", "NuevaPass123!", "123456")));
    }

    [Fact]
    public async Task Desvincular_authenticator_con_contrasena_incorrecta_falla()
    {
        var user = BuildUser(twoFactorEnabled: true);
        var userManager = UserManagerFor(user);
        userManager.Setup(m => m.CheckPasswordAsync(user, "mala")).ReturnsAsync(false);

        var command = new ResetAuthenticatorCommandImpl(userManager.Object, new FakeCurrentUser(user.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new ResetAuthenticatorRequest("mala", "123456")));

        // La clave no se rota: si lo hiciera, cualquiera con la sesión abierta dejaría al dueño
        // fuera de su propia app de autenticación.
        userManager.Verify(m => m.ResetAuthenticatorKeyAsync(It.IsAny<ApplicationUser>()), Times.Never);
        userManager.Verify(m => m.SetTwoFactorEnabledAsync(It.IsAny<ApplicationUser>(), false), Times.Never);
    }

    [Fact]
    public async Task Avatar_que_no_es_jpeg_se_rechaza()
    {
        var db = new TrackingTasksDbContext(
            new DbContextOptionsBuilder<TrackingTasksDbContext>()
                .UseInMemoryDatabase(nameof(Avatar_que_no_es_jpeg_se_rechaza))
                .Options);

        var command = new UpdateAvatarCommandImpl(db, new FakeCurrentUser("user-1"));

        // PNG: magic bytes 89 50 4E 47. Base64 válido, imagen válida, formato equivocado.
        var png = Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        await Assert.ThrowsAsync<ValidationException>(() =>
            command.Execute(new UpdateAvatarRequest(png)));

        Assert.Empty(db.UserAvatars);
    }
}
