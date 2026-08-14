using System.Net;
using System.Text.Json;
using Application.Dto.TimeEntry;
using Infrastructure.Adapters.UseCases.TimeEntry;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.TimeEntry;

public class AddTimeEntryCommandImplTests
{
    private static (AddTimeEntryCommandImpl command, List<string> bodies) BuildCommand()
    {
        var bodies = new List<string>();
        var handler = new Mock<HttpMessageHandler>();

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage req, CancellationToken _) =>
            {
                bodies.Add(await req.Content!.ReadAsStringAsync());
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}") };
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost:8080") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return (new AddTimeEntryCommandImpl(factory.Object, NullLogger<AddTimeEntryCommandImpl>.Instance), bodies);
    }

    [Fact]
    public async Task Execute_ConSpentOn_UsaEsaFechaEnElPayload()
    {
        var (command, bodies) = BuildCommand();

        await command.Execute(new AddTimeEntryRequest(1134, 5, 1.5, "trabajo", new DateOnly(2026, 3, 9)));

        using var doc = JsonDocument.Parse(bodies.Single());
        Assert.Equal("2026-03-09", doc.RootElement.GetProperty("spentOn").GetString());
    }

    [Fact]
    public async Task Execute_SinSpentOn_UsaLaFechaDeHoy()
    {
        var (command, bodies) = BuildCommand();

        await command.Execute(new AddTimeEntryRequest(1134, 5, 1.5, "trabajo"));

        using var doc = JsonDocument.Parse(bodies.Single());
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), doc.RootElement.GetProperty("spentOn").GetString());
    }
}
