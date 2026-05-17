using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Tests.Infrastructure.Adapters.UseCases.WorkPackages;

public class UpdateWorkPackageCommandImplTests
{
    /// <summary>
    /// Construye el use case con un HttpMessageHandler mockeado.
    /// GET → devuelve {"lockVersion":3} (simula la obtención del lock).
    /// PATCH → captura el body enviado y devuelve <paramref name="patchStatus"/>.
    /// </summary>
    private static (UpdateWorkPackageCommandImpl useCase, Func<string?> getCapturedBody)
        BuildUseCase(HttpStatusCode patchStatus = HttpStatusCode.OK)
    {
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                if (req.Method == HttpMethod.Get)
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"lockVersion":3}""", Encoding.UTF8, "application/json")
                    };

                capturedBody = await req.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(patchStatus)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("http://localhost") };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var logger  = new Mock<ILogger<UpdateWorkPackageCommandImpl>>();
        var useCase = new UpdateWorkPackageCommandImpl(factoryMock.Object, logger.Object);

        return (useCase, () => capturedBody);
    }

    // ── lockVersion ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_AlwaysIncludesLockVersionFromGet()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, percentageDone: 10);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal(3, node["lockVersion"]!.GetValue<int>());
    }

    // ── percentageDone ────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithPercentageDone_PayloadIncludesField()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, percentageDone: 75);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal(75, node["percentageDone"]!.GetValue<int>());
    }

    [Fact]
    public async Task Execute_WithoutPercentageDone_PayloadExcludesField()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, statusId: 2);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.False(node.AsObject().ContainsKey("percentageDone"));
    }

    // ── startDate / dueDate ───────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithStartDate_PayloadIncludesStartDate()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, startDate: "2026-05-01");

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal("2026-05-01", node["startDate"]!.GetValue<string>());
    }

    [Fact]
    public async Task Execute_WithDueDate_PayloadIncludesDueDate()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, dueDate: "2026-05-31");

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal("2026-05-31", node["dueDate"]!.GetValue<string>());
    }

    [Fact]
    public async Task Execute_WithNoChangeSentinel_PayloadExcludesDateFields()
    {
        var (useCase, getBody) = BuildUseCase();

        // Llamada con los defaults: startDate = NoChange, dueDate = NoChange
        await useCase.Execute(1, percentageDone: 50);

        var node = JsonNode.Parse(getBody()!)!.AsObject();
        Assert.False(node.ContainsKey("startDate"));
        Assert.False(node.ContainsKey("dueDate"));
    }

    [Fact]
    public async Task Execute_WithEmptyStartDate_PayloadSetsStartDateToNull()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, startDate: "");

        var node = JsonNode.Parse(getBody()!)!.AsObject();
        Assert.True(node.ContainsKey("startDate"));
        Assert.Null(node["startDate"]);
    }

    [Fact]
    public async Task Execute_WithEmptyDueDate_PayloadSetsDueDateToNull()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, dueDate: "");

        var node = JsonNode.Parse(getBody()!)!.AsObject();
        Assert.True(node.ContainsKey("dueDate"));
        Assert.Null(node["dueDate"]);
    }

    // ── _links ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_WithStatusId_PayloadIncludesStatusLink()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, statusId: 5);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal("/api/v3/statuses/5", node["_links"]!["status"]!["href"]!.GetValue<string>());
    }

    [Fact]
    public async Task Execute_WithStatusIdZero_PayloadExcludesStatusLink()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, statusId: 0);

        var node = JsonNode.Parse(getBody()!)!;
        // statusId == 0 no agrega el link, y si no hay otros links tampoco hay _links
        var links = node["_links"]?.AsObject();
        Assert.True(links is null || !links.ContainsKey("status"));
    }

    [Fact]
    public async Task Execute_WithAssigneeId_PayloadIncludesAssigneeLink()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, assigneeId: 7);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.Equal("/api/v3/users/7", node["_links"]!["assignee"]!["href"]!.GetValue<string>());
    }

    [Fact]
    public async Task Execute_WithAssigneeIdZero_PayloadSetsAssigneeLinkToNull()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, assigneeId: 0);

        var node = JsonNode.Parse(getBody()!)!;
        // assigneeId == 0 → link explícitamente null (para desasignar)
        Assert.True(node["_links"]!.AsObject().ContainsKey("assignee"));
        Assert.Null(node["_links"]!["assignee"]);
    }

    [Fact]
    public async Task Execute_WithNoLinkParams_PayloadExcludesLinksObject()
    {
        var (useCase, getBody) = BuildUseCase();

        await useCase.Execute(1, percentageDone: 30);

        var node = JsonNode.Parse(getBody()!)!;
        Assert.False(node.AsObject().ContainsKey("_links"));
    }

    // ── error handling ────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ErrorResponse_ThrowsException()
    {
        var (useCase, _) = BuildUseCase(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<Exception>(() => useCase.Execute(1, statusId: 2));
    }

    [Fact]
    public async Task Execute_UnauthorizedResponse_ThrowsException()
    {
        var (useCase, _) = BuildUseCase(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<Exception>(() => useCase.Execute(1, statusId: 2));
    }
}
