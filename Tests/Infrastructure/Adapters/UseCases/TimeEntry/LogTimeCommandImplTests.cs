using System.ComponentModel.DataAnnotations;
using Application.Dto.TimeEntry;
using Infrastructure.Adapters.UseCases.TimeEntry;

namespace Tests.Infrastructure.Adapters.UseCases.TimeEntry;

/// <summary>
/// Registro manual de tiempo: resolución de horas y validaciones de entrada.
/// Es un boundary de usuario, así que los casos límite se cubren explícitamente.
/// </summary>
public class LogTimeCommandImplTests
{
    private static LogTimeRequest Req(double? hours = null, TimeOnly? start = null, TimeOnly? end = null)
        => new(1, new DateOnly(2026, 8, 14), hours, StartTime: start, EndTime: end);

    [Fact]
    public void ResolveHours_ConHorasExplicitas_LasUsa()
    {
        Assert.Equal(2.5, LogTimeCommandImpl.ResolveHours(Req(hours: 2.5)));
    }

    [Fact]
    public void ResolveHours_SinHoras_LasCalculaDelRango()
    {
        var hours = LogTimeCommandImpl.ResolveHours(
            Req(start: new TimeOnly(14, 0), end: new TimeOnly(17, 30)));

        Assert.Equal(3.5, hours);
    }

    [Fact]
    public void ResolveHours_ConHorasYRango_LasHorasMandan()
    {
        // El usuario pudo corregir el campo de horas: no se pisa con el rango.
        var hours = LogTimeCommandImpl.ResolveHours(
            Req(hours: 2, start: new TimeOnly(9, 0), end: new TimeOnly(17, 0)));

        Assert.Equal(2, hours);
    }

    [Fact]
    public void ResolveHours_RangoInvertido_Falla()
    {
        var ex = Assert.Throws<ValidationException>(() => LogTimeCommandImpl.ResolveHours(
            Req(start: new TimeOnly(17, 0), end: new TimeOnly(9, 0))));

        Assert.Contains("posterior", ex.Message);
    }

    [Fact]
    public void ResolveHours_RangoDeDuracionCero_Falla()
    {
        Assert.Throws<ValidationException>(() => LogTimeCommandImpl.ResolveHours(
            Req(start: new TimeOnly(9, 0), end: new TimeOnly(9, 0))));
    }

    [Fact]
    public void ResolveHours_SinHorasNiRango_PideLosDatos()
    {
        var ex = Assert.Throws<ValidationException>(() => LogTimeCommandImpl.ResolveHours(Req()));
        Assert.Contains("horas", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveHours_SoloUnExtremoDelRango_PideLosDatos()
    {
        // Con solo la hora de inicio no se puede deducir nada.
        Assert.Throws<ValidationException>(() =>
            LogTimeCommandImpl.ResolveHours(Req(start: new TimeOnly(9, 0))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ResolveHours_HorasNoPositivas_Falla(double hours)
    {
        Assert.Throws<ValidationException>(() => LogTimeCommandImpl.ResolveHours(Req(hours)));
    }

    [Fact]
    public void ResolveHours_MasDe24Horas_Falla()
    {
        // Un día no da para más: casi siempre es un error de tipeo (ej. 80 en vez de 8).
        var ex = Assert.Throws<ValidationException>(() => LogTimeCommandImpl.ResolveHours(Req(80)));
        Assert.Contains("24", ex.Message);
    }

    [Fact]
    public void ResolveHours_Exactamente24Horas_SeAcepta()
    {
        Assert.Equal(24, LogTimeCommandImpl.ResolveHours(Req(24)));
    }

    [Fact]
    public void ResolveHours_RedondeaADosDecimales()
    {
        // 20 minutos = 0.333…h; OpenProject no necesita más precisión que el centésimo.
        var hours = LogTimeCommandImpl.ResolveHours(
            Req(start: new TimeOnly(9, 0), end: new TimeOnly(9, 20)));

        Assert.Equal(0.33, hours);
    }
}
