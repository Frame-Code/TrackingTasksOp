using System.Net;
using System.Text;
using Application.Ports.Auth;
using Infrastructure.Adapters.Services.Bot;
using Infrastructure.DataAccess.Entities;
using Infrastructure.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqTranscriptionClientTests
{
    private class NoUserCurrentUser : CurrentUser
    {
        public override string? UserId => null;
        public override bool IsAuthenticated => false;
        public override string? OpenProjectInstanceUrl => null;
        public override int? OpenProjectInstanceId => null;
        public override int? OpenProjectUserId => null;
    }

    // Sin usuario autenticado, GroqAuthHeaderProvider cae siempre a la key compartida de
    // GroqSettings — exactamente lo que estos tests ya esperan.
    private static GroqAuthHeaderProvider BuildAuthHeaderProvider(GroqSettings settings)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
#pragma warning disable CS8625
        var userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
        return new GroqAuthHeaderProvider(
            userManager.Object, new NoUserCurrentUser(), Mock.Of<IApiKeyEncryptorService>(), Options.Create(settings));
    }

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

        var effectiveSettings = settings ?? BuildSettings();
        return new GroqTranscriptionClient(factoryMock.Object, Options.Create(effectiveSettings), BuildAuthHeaderProvider(effectiveSettings));
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
        var client = new GroqTranscriptionClient(Mock.Of<IHttpClientFactory>(), Options.Create(settings), BuildAuthHeaderProvider(settings));

        Assert.Equal(expected, client.IsConfigured);
    }
}
