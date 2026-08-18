using System.Net;
using System.Text;
using System.Web;
using Application.Dto.ListWorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.WorkPackages;

public class ListsWorkPackagesCommandImplTests
{
    private const string EmptyCollectionJson = """
        {
            "total": 0,
            "count": 0,
            "_embedded": { "elements": [] }
        }
        """;

    private (ListsWorkPackagesCommandImpl Command, Func<Uri?> GetCapturedUri) BuildCommand()
    {
        Uri? capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(EmptyCollectionJson, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var logger = new Mock<ILogger<ListsWorkPackagesCommandImpl>>();
        var command = new ListsWorkPackagesCommandImpl(factoryMock.Object, logger.Object);

        return (command, () => capturedUri);
    }

    [Fact]
    public async Task Execute_WithStatusId_ShouldBuildUrlWithExactStatusFilter()
    {
        var (command, getUri) = BuildCommand();

        await command.Execute(new ListsWorkPackagesRequest(null, 0, 50, StatusId: 5));

        var filters = HttpUtility.UrlDecode(HttpUtility.ParseQueryString(getUri()!.Query)["filters"]);
        Assert.Contains("\"status\":{\"operator\":\"=\",\"values\":[\"5\"]}", filters);
        Assert.Contains("\"assignee\":{\"operator\":\"=\",\"values\":[\"me\"]}", filters);
    }

    [Fact]
    public async Task Execute_WithoutStatusId_ShouldBuildUrlWithAllStatusesFilter()
    {
        var (command, getUri) = BuildCommand();

        await command.Execute(new ListsWorkPackagesRequest(null, 0, 50));

        var filters = HttpUtility.UrlDecode(HttpUtility.ParseQueryString(getUri()!.Query)["filters"]);
        Assert.Contains("\"status\":{\"operator\":\"*\",\"values\":[]}", filters);
    }
}
