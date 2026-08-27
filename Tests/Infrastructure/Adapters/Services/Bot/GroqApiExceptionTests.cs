using System.Globalization;
using System.Net;
using Infrastructure.Adapters.Services.Bot;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqApiExceptionTests
{
    // Cuerpos reales devueltos por Groq en producción: si cambian el formato, estos tests
    // son los que avisan, no el usuario en medio de una demo.
    private const string RateLimitBody =
        """
        {"error":{"message":"Rate limit reached for model `openai/gpt-oss-120b` in organization `org_01kngd` service tier `on_demand` on tokens per minute (TPM): Limit 8000, Used 1443, Requested 7227. Please try again in 5.025s. Need more tokens? Upgrade to Dev Tier today at https://console.groq.com/settings/billing","type":"tokens","code":"rate_limit_exceeded"}}
        """;

    private const string ToolValidationBody =
        """
        {"error":{"message":"Tool call validation failed: tool call validation failed: attempted to call tool 'list_tasks' which was not in request.tools","type":"invalid_request_error","code":"tool_use_failed"}}
        """;

    [Fact]
    public void RateLimit_SeClasificaYSeLeeLaEsperaSugerida()
    {
        var ex = GroqApiException.FromResponse(HttpStatusCode.TooManyRequests, RateLimitBody);

        Assert.Equal(GroqFailureKind.RateLimited, ex.Kind);
        Assert.Equal(TimeSpan.FromSeconds(5.025), ex.RetryAfter);
    }

    [Fact]
    public void ToolValidation_SeClasificaYNoTraeEspera()
    {
        var ex = GroqApiException.FromResponse(HttpStatusCode.BadRequest, ToolValidationBody);

        Assert.Equal(GroqFailureKind.ToolValidation, ex.Kind);
        // No hay espera que respetar: el reintento va inmediato, pero sin el array "tools".
        Assert.Null(ex.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public void ProblemasDeCredenciales_SeClasificanComoAuthentication(HttpStatusCode status)
    {
        var ex = GroqApiException.FromResponse(status, """{"error":{"message":"Invalid API Key"}}""");

        Assert.Equal(GroqFailureKind.Authentication, ex.Kind);
    }

    [Fact]
    public void BadRequestQueNoEsDeTools_QuedaComoUnknown()
    {
        // Importante que NO sea ToolValidation: reintentar sin tools no arreglaría nada y
        // gastaría cupo del plan sin motivo.
        var ex = GroqApiException.FromResponse(HttpStatusCode.BadRequest, """{"error":{"message":"model not found"}}""");

        Assert.Equal(GroqFailureKind.Unknown, ex.Kind);
    }

    [Fact]
    public void RateLimitSinEsperaIndicada_NoInventaUnaEspera()
    {
        var ex = GroqApiException.FromResponse(HttpStatusCode.TooManyRequests,
            """{"error":{"message":"Rate limit reached","code":"rate_limit_exceeded"}}""");

        Assert.Equal(GroqFailureKind.RateLimited, ex.Kind);
        // Sin dato de Groq no se reintenta: mejor un mensaje claro que una espera adivinada.
        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public void LaEsperaSeLeeIgualEnUnaCulturaDeComaDecimal()
    {
        // El servidor corre en Ecuador (es-EC), donde el separador decimal es la coma. Sin
        // InvariantCulture, "5.025s" se leería como 5025 segundos y el reintento colgaría el
        // request casi una hora y media.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-EC");
            var ex = GroqApiException.FromResponse(HttpStatusCode.TooManyRequests, RateLimitBody);

            Assert.Equal(TimeSpan.FromSeconds(5.025), ex.RetryAfter);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ElCuerpoCrudoSeConservaParaElLog()
    {
        var ex = GroqApiException.FromResponse(HttpStatusCode.TooManyRequests, RateLimitBody);

        // Se conserva para diagnosticar; quien decide no mostrárselo al usuario es GroqIntentService.
        Assert.Equal(RateLimitBody, ex.Body);
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }
}
