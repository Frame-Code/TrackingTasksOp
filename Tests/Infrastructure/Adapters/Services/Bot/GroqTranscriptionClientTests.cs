using System.Net;
using System.Text;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqTranscriptionClientTests
{
    private static GroqSettings BuildSettings(string apiKey = "fake-key") => new()
    {
        ApiKey = apiKey,
        Model = "llama-3.3-70b-versatile",
        Temperature = 0.1f,
        BaseUrl = "https://api.groq.com/openai/v1/chat/completions",
        TranscriptionModel = "whisper-large-v3-turbo",
        TranscriptionBaseUrl = "https://api.groq.com/openai/v1/audio/transcriptions",
        TranscriptionLanguage = "es"
    };

    private GroqTranscriptionClient BuildClient(
        HttpStatusCode status,
        string responseBody,
        out Mock<HttpMessageHandler> handlerMock,
        GroqSettings? settings = null)
    {
        handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(client);

        return new GroqTranscriptionClient(factoryMock.Object, Options.Create(settings ?? BuildSettings()));
    }

    [Fact]
    public async Task TranscribeAsync_SuccessfulRequest_ReturnsTranscript()
    {
        const string json = """{ "text": "  pausa la tarea 1134  " }""";
        var client = BuildClient(HttpStatusCode.OK, json, out _);

        using var audio = new MemoryStream([1, 2, 3, 4]);
        var result = await client.TranscribeAsync(audio, "audio.webm", "audio/webm");

        Assert.Equal("pausa la tarea 1134", result);
    }

    [Fact]
    public async Task TranscribeAsync_SuccessfulRequest_SendsExpectedMultipartFields()
    {
        const string json = """{ "text": "hola" }""";
        var client = BuildClient(HttpStatusCode.OK, json, out var handlerMock);

        using var audio = new MemoryStream([1, 2, 3, 4]);
        await client.TranscribeAsync(audio, "audio.webm", "audio/webm");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri == new Uri("https://api.groq.com/openai/v1/audio/transcriptions") &&
                req.Content is MultipartFormDataContent),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TranscribeAsync_ErrorResponse_ThrowsException(HttpStatusCode statusCode)
    {
        var client = BuildClient(statusCode, """{ "error": "boom" }""", out _);

        using var audio = new MemoryStream([1, 2, 3, 4]);
        await Assert.ThrowsAsync<Exception>(() => client.TranscribeAsync(audio, "audio.webm", "audio/webm"));
    }

    [Fact]
    public async Task TranscribeAsync_ResponseWithoutTextProperty_ReturnsEmptyString()
    {
        var client = BuildClient(HttpStatusCode.OK, "{}", out _);

        using var audio = new MemoryStream([1, 2, 3, 4]);
        var result = await client.TranscribeAsync(audio, "audio.webm", "audio/webm");

        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("fake-key", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsConfigured_ReflectsApiKeyPresence(string? apiKey, bool expected)
    {
        var settings = BuildSettings(apiKey!);
        var client = new GroqTranscriptionClient(Mock.Of<IHttpClientFactory>(), Options.Create(settings));

        Assert.Equal(expected, client.IsConfigured);
    }
}
