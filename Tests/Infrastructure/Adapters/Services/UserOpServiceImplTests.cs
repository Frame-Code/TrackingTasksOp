using System.Net;
using System.Text;
using Infrastructure.Adapters.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class UserOpServiceImplTests
{
    private const string AssigneesJson = """
        {
            "_embedded": {
                "elements": [
                    { "id": 30, "name": "Stin Sanchez", "login": "stin.sanchez@viaxperta.com", "email": "stin.sanchez@viaxperta.com" },
                    { "id": 31, "name": "Otro Usuario", "login": "otro@viaxperta.com", "email": "otro@viaxperta.com" }
                ]
            }
        }
        """;

    private static (UserOpServiceImpl Service, Func<HttpRequestMessage?> GetCapturedRequest) BuildService(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var logger = new Mock<ILogger<UserOpServiceImpl>>();
        var service = new UserOpServiceImpl(factoryMock.Object, logger.Object);

        return (service, () => capturedRequest);
    }

    [Fact]
    public async Task ListAssignees_ShouldQueryProjectScopedEndpoint()
    {
        var (service, getRequest) = BuildService(AssigneesJson);

        var users = await service.ListAssignees(4);

        Assert.Equal(2, users.Count);
        Assert.Equal("/api/v3/projects/4/available_assignees", getRequest()!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task FindAssigneeByName_ShouldReturnPartialMatch()
    {
        var (service, _) = BuildService(AssigneesJson);

        var user = await service.FindAssigneeByName(4, "Stin");

        Assert.NotNull(user);
        Assert.Equal(30, user!.Id);
    }

    [Fact]
    public async Task FindAssigneeByName_NoMatch_ShouldReturnNull()
    {
        var (service, _) = BuildService(AssigneesJson);

        var user = await service.FindAssigneeByName(4, "Inexistente");

        Assert.Null(user);
    }

    [Fact]
    public async Task ListAssignees_OnForbidden_ShouldReturnEmptyList()
    {
        var (service, _) = BuildService("{}", HttpStatusCode.Forbidden);

        var users = await service.ListAssignees(4);

        Assert.Empty(users);
    }
}
