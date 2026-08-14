using System.Net;
using Infrastructure.Adapters.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.Services;

public class TimeEntryOpServiceImplTests
{
    private const string PageOne = """
    {
      "_type": "Collection", "total": 3, "count": 2, "pageSize": 200, "offset": 1,
      "_embedded": { "elements": [
        { "id": 1, "spentOn": "2026-03-09", "hours": "PT1H30M",
          "_links": { "workPackage": { "href": "/api/v3/work_packages/1134", "title": "Login" },
                      "project": { "href": "/api/v3/projects/5", "title": "eProduction" },
                      "activity": { "href": "/api/v3/time_entries/activities/1", "title": "Development" },
                      "user": { "href": "/api/v3/users/7", "title": "Stin" } } },
        { "id": 2, "spentOn": "2026-03-09", "hours": "PT30M",
          "_links": { "workPackage": { "href": "/api/v3/work_packages/1134", "title": "Login" },
                      "project": { "href": "/api/v3/projects/5", "title": "eProduction" },
                      "activity": { "href": "/api/v3/time_entries/activities/1", "title": "Development" },
                      "user": { "href": "/api/v3/users/7", "title": "Stin" } } },
        { "id": 3, "spentOn": "2026-02-14", "hours": "PT2H",
          "_links": { "workPackage": { "href": "/api/v3/work_packages/990", "title": "Reporte" },
                      "project": { "href": "/api/v3/projects/5", "title": "eProduction" },
                      "activity": { "href": "/api/v3/time_entries/activities/2", "title": "Management" },
                      "user": { "href": "/api/v3/users/7", "title": "Stin" } } }
      ] }
    }
    """;

    private static (TimeEntryOpServiceImpl service, List<string> urls) BuildService(string page)
    {
        var urls = new List<string>();
        var handler = new Mock<HttpMessageHandler>();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage req, CancellationToken _) =>
            {
                urls.Add(req.RequestUri!.PathAndQuery);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page) });
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:8080") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return (new TimeEntryOpServiceImpl(factory.Object, NullLogger<TimeEntryOpServiceImpl>.Instance), urls);
    }

    [Fact]
    public async Task Lists_ConvierteLaDuracionIso8601AHorasDecimales()
    {
        var (service, _) = BuildService(PageOne);

        var entries = await service.Lists(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31), 7);

        Assert.Equal(1.5, entries[0].HoursAsDouble);
        Assert.Equal(0.5, entries[1].HoursAsDouble);
        Assert.Equal(2.0, entries[2].HoursAsDouble);
    }

    [Fact]
    public async Task Lists_ExtraeElWorkPackageIdDelHref()
    {
        var (service, _) = BuildService(PageOne);

        var entries = await service.Lists(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31), 7);

        Assert.Equal(1134, entries[0].WorkPackageId);
        Assert.Equal(990, entries[2].WorkPackageId);
    }

    [Fact]
    public async Task Lists_FiltraPorRangoDeFechasYUsuario()
    {
        var (service, urls) = BuildService(PageOne);

        await service.Lists(new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 31), 7);

        var decoded = Uri.UnescapeDataString(urls[0]);
        Assert.Contains("2026-02-01", decoded);
        Assert.Contains("2026-03-31", decoded);
        Assert.Contains("\"user\"", decoded);
    }
}
