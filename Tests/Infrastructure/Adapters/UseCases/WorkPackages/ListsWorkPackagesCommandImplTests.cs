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
        var command = new ListsWorkPackagesCommandImpl(factoryMock.Object, logger.Object, new global::Infrastructure.Adapters.Http.RequestTimings());

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

    /// <summary>
    /// La pantalla pide UNA pagina: si pidiera todas, filtrar en el servidor no serviria
    /// de nada. Se verifica que solo salga una peticion y con el pageSize solicitado.
    /// </summary>
    [Fact]
    public async Task ExecutePageAsync_PideUnaSolaPaginaConElTamanoIndicado()
    {
        var (command, getUri) = BuildCommand();

        var result = await command.ExecutePageAsync(new ListsWorkPackagesRequest(null, 3, 12));

        var query = HttpUtility.ParseQueryString(getUri()!.Query);
        Assert.Equal("12", query["pageSize"]);
        Assert.Equal("3", query["offset"]);   // en OpenProject `offset` es el numero de pagina
        Assert.Equal(3, result.Page);
        Assert.Equal(12, result.PageSize);
    }

    [Fact]
    public async Task ExecutePageAsync_ConVariosEstadosYBusqueda_LosMandaAOpenProject()
    {
        var (command, getUri) = BuildCommand();

        await command.ExecutePageAsync(new ListsWorkPackagesRequest(
            null, 1, 12, StatusIds: [7, 12], Search: "lanec"));

        var filters = HttpUtility.UrlDecode(HttpUtility.ParseQueryString(getUri()!.Query)["filters"]);
        Assert.Contains("\"status\":{\"operator\":\"=\",\"values\":[\"7\",\"12\"]}", filters);
        Assert.Contains("lanec", filters);
    }
}
