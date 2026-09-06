using System.Net;
using System.Text;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.WorkPackages;

public class GetWorkPackageChildrenQueryImplTests
{
    private const string ChildrenJson = """
        {
            "total": 2,
            "_embedded": {
                "elements": [
                    { "id": 431, "subject": "Formularios", "_links": { "parent": { "href": "/api/v3/work_packages/418" } } },
                    { "id": 433, "subject": "Acta de firma" }
                ]
            }
        }
        """;

    private static (GetWorkPackageChildrenQueryImpl Query, Func<HttpRequestMessage?> GetRequest) BuildQuery(
        string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        HttpRequestMessage? captured = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var query = new GetWorkPackageChildrenQueryImpl(
            factoryMock.Object, new Mock<ILogger<GetWorkPackageChildrenQueryImpl>>().Object);

        return (query, () => captured);
    }

    [Fact]
    public async Task ExecuteAsync_NoDebeFiltrarPorAsignado()
    {
        // El árbol muestra los hijos de CUALQUIER persona. Si alguien reusa el builder del
        // listado de tareas (que filtra por "me"), esto se rompe en silencio: el árbol
        // seguiría funcionando, pero mostrando solo lo propio.
        var (query, getRequest) = BuildQuery(ChildrenJson);

        await query.ExecuteAsync(418);

        var url = Uri.UnescapeDataString(getRequest()!.RequestUri!.ToString());
        Assert.Contains("\"parent\":{\"operator\":\"=\",\"values\":[\"418\"]}", url);
        Assert.DoesNotContain("assignee", url);
    }

    [Fact]
    public async Task ExecuteAsync_DevuelveLosHijos()
    {
        var (query, _) = BuildQuery(ChildrenJson);

        var children = await query.ExecuteAsync(418);

        Assert.Equal([431, 433], children.Select(c => c.Id));
        Assert.Equal(418, children[0].Links.Parent.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ParentInvalido_NoLlamaAOpenProject()
    {
        var (query, getRequest) = BuildQuery(ChildrenJson);

        var children = await query.ExecuteAsync(0);

        Assert.Empty(children);
        Assert.Null(getRequest());
    }

    [Fact]
    public async Task ExecuteAsync_CuandoOpenProjectFalla_PropagaSuMotivo()
    {
        var (query, _) = BuildQuery("""{ "message": "No tenés permiso para ver esta tarea." }""", HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<Exception>(() => query.ExecuteAsync(418));

        Assert.Equal("No tenés permiso para ver esta tarea.", ex.Message);
    }
}
