using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Web.Infrastructure.Adapters.Services;
using Web.Infrastructure.Config.Settings;

namespace Tests.Infrastructure.Adapters.Services;

public class ActivityOpServiceTests
{
    private ActivityOpServiceImpl BuildService(HttpStatusCode status, string jsonResponse)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
                )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
        
        var client = new HttpClient(handlerMock.Object);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(client);

        var settings = Options.Create(new OpenProjectSettings
        {
            BaseUrl = "https://fake.open-project.com",
            HttpClientName = "op"
        });

        var logger = new Mock<ILogger<ActivityOpServiceImpl>>();
        return new ActivityOpServiceImpl(factoryMock.Object, logger.Object, settings);
    }

    [Fact]
    public async Task Lists_SuccessfulRequest_ReturnActivities()
    {
        const string json = """
                            {
                                "_embedded": {
                                    "schema": {
                                        "activity": {
                                            "_embedded": {
                                                "allowedValues": [
                                                    { "id": 1, "name": "Design" },
                                                    { "id": 2, "name": "Development" }
                                                ]
                                            }
                                        }
                                    }
                                }
                            }
                            """;
        
        var service = BuildService(HttpStatusCode.OK, json);
        var resultado = await service.Lists(42);
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Equal("Design", resultado[0].Name);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Lists_AccessDenied_ReturnEmptyList(HttpStatusCode statusCode)
    {
        var service = BuildService(statusCode, string.Empty);
        var resultado = await service.Lists(42);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Lists_BadResponse_ReturnEmptyList()
    {
        var service = BuildService(HttpStatusCode.BadRequest, string.Empty);
        var resultado = await service.Lists(42);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Lists_CanNotDeserialize_ReturnEmptyList()
    {
        const string json = """
                            {
                                "bad_embedded": {
                                    "bad_schema": {
                                        "activity": {
                                            "_embedded": {
                                                "bad_allowedValues": [
                                                    { "id": 1, "name": "Design" },
                                                    { "id": 2, "name": "Development" }
                                                ]
                                            }
                                        }
                                    }
                                }
                            }
                            """;

        var service = BuildService(HttpStatusCode.Accepted, json);
        var response = await service.Lists(86);
        Assert.Empty(response);
    }
}