using Infrastructure.Adapters.Services;

namespace Tests.Infrastructure.Adapters.Services;

public class TimeTrackServiceTests
{
    [Theory]
    // Ya cae justo en un bloque: no se toca. Es el caso que el algoritmo anterior rompía,
    // porque 30 min entraban en la rama de 10-20 minutos extra.
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    [InlineData(120, 120)]
    // Sube al siguiente bloque.
    [InlineData(1, 15)]
    [InlineData(14, 15)]
    [InlineData(16, 30)]
    [InlineData(32, 45)]
    [InlineData(46, 60)]
    // 2 h exactas no recibían nada antes (TimeSpan.Minutes == 0); ahora tampoco, pero por la
    // razón correcta: ya es múltiplo de 15.
    [InlineData(118, 120)]
    [InlineData(121, 135)]
    public void RoundUpToQuarterHour_RedondeaAlSiguienteBloqueDe15(int minutosTrackeados, int minutosEsperados)
    {
        var resultado = TimeTrackService.RoundUpToQuarterHour(TimeSpan.FromMinutes(minutosTrackeados));

        Assert.Equal(TimeSpan.FromMinutes(minutosEsperados), resultado);
    }

    [Fact]
    public void RoundUpToQuarterHour_DescartaLosSegundos_NoSaltaBloquePorUnosSegundos()
    {
        // Parar el cronómetro unos segundos después de los 30 minutos exactos no debe costar
        // un bloque entero.
        var resultado = TimeTrackService.RoundUpToQuarterHour(new TimeSpan(0, 30, 20));

        Assert.Equal(TimeSpan.FromMinutes(30), resultado);
    }

    [Fact]
    public void RoundUpToQuarterHour_SesionDeMenosDeUnMinuto_NoInventaTiempo()
    {
        // Un clic accidental no son 15 minutos de trabajo.
        Assert.Equal(TimeSpan.Zero, TimeTrackService.RoundUpToQuarterHour(TimeSpan.FromSeconds(40)));
        Assert.Equal(TimeSpan.Zero, TimeTrackService.RoundUpToQuarterHour(TimeSpan.Zero));
    }

    [Fact]
    public void RoundUpToQuarterHour_NuncaDevuelveMenosQueLoTrackeado()
    {
        // Es un margen: puede sumar, nunca restar. Se recorre minuto a minuto una jornada.
        for (var minutos = 1; minutos <= 8 * 60; minutos++)
        {
            var trackeado = TimeSpan.FromMinutes(minutos);
            var resultado = TimeTrackService.RoundUpToQuarterHour(trackeado);

            Assert.True(resultado >= trackeado, $"{minutos} min quedó por debajo de lo trackeado");
            Assert.True(resultado - trackeado < TimeSpan.FromMinutes(15),
                $"{minutos} min sumó un bloque entero o más");
        }
    }
}
