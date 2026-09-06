using System.Net;
using System.Text;
using Application.Dto.WorkPackages;
using Infrastructure.Adapters.UseCases.WorkPackages;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Task = System.Threading.Tasks.Task;

namespace Tests.Infrastructure.Adapters.UseCases.WorkPackages;

public class CreateWorkPackageCommandImplTests
{
    private const string FormSchemaJson = """
        {
            "_embedded": {
                "schema": {
                    "_type": "Schema",
                    "subject": { "type": "String", "required": true },
                    "customField3": {
                        "type": "CustomOption",
                        "name": "Area",
                        "required": true,
                        "_embedded": {
                            "allowedValues": [
                                { "_type": "CustomOption", "id": 7, "value": "PRODUCCION" },
                                { "_type": "CustomOption", "id": 8, "value": "ADMINISTRACION" }
                            ]
                        }
                    },
                    "customField5": {
                        "type": "CustomOption",
                        "name": "Modulo",
                        "required": true,
                        "_embedded": {
                            "allowedValues": [
                                { "_type": "CustomOption", "id": 11, "value": "Backend" },
                                { "_type": "CustomOption", "id": 12, "value": "Frontend" }
                            ]
                        }
                    },
                    "customField7": {
                        "type": "CustomOption",
                        "name": "Categoria",
                        "required": false,
                        "_embedded": { "allowedValues": [] }
                    },
                    "priority": { "type": "Priority", "required": true }
                }
            }
        }
        """;

    private static (CreateWorkPackageCommandImpl Command, Func<HttpRequestMessage?> GetCapturedRequest) BuildCommand(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
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

        var logger = new Mock<ILogger<CreateWorkPackageCommandImpl>>();
        var command = new CreateWorkPackageCommandImpl(factoryMock.Object, logger.Object);

        return (command, () => capturedRequest);
    }

    [Fact]
    public async Task GetRequiredCustomFieldsAsync_ShouldReturnOnlyRequiredCustomOptionFields()
    {
        var (command, getRequest) = BuildCommand(FormSchemaJson);

        var result = await command.GetRequiredCustomFieldsAsync(4, 1);

        Assert.Equal(2, result.Count);

        var area = result.Single(f => f.Key == "customField3");
        Assert.Equal("Area", area.Name);
        Assert.Equal(["PRODUCCION", "ADMINISTRACION"], area.AllowedValues.Select(o => o.Value));
        Assert.Contains(area.AllowedValues, o => o.Id == 7 && o.Value == "PRODUCCION");

        var modulo = result.Single(f => f.Key == "customField5");
        Assert.Equal("Modulo", modulo.Name);
        Assert.Equal(["Backend", "Frontend"], modulo.AllowedValues.Select(o => o.Value));

        Assert.DoesNotContain(result, f => f.Key == "customField7"); // no requerido
        Assert.DoesNotContain(result, f => f.Key == "priority"); // no es CustomOption

        var request = getRequest();
        Assert.NotNull(request);
        Assert.Equal("/api/v3/projects/4/work_packages/form", request!.RequestUri!.AbsolutePath);
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("/api/v3/types/1", body);
    }

    [Fact]
    public async Task GetRequiredCustomFieldsAsync_OnError_ShouldReturnEmptyList()
    {
        var (command, _) = BuildCommand("{}", HttpStatusCode.InternalServerError);

        var result = await command.GetRequiredCustomFieldsAsync(4, 1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Execute_WithCustomFieldOptionIds_ShouldIncludeThemAsLinks()
    {
        const string createdWorkPackageJson = """
            {
                "id": 999,
                "subject": "Tarea de prueba"
            }
            """;
        var (command, getRequest) = BuildCommand(createdWorkPackageJson);

        var request = new CreateWorkPackageRequest(
            Subject: "Tarea de prueba",
            ProjectId: 4,
            CustomFieldOptionIds: new Dictionary<string, int> { ["customField3"] = 7, ["customField5"] = 11 });

        var workPackage = await command.Execute(request);

        Assert.Equal(999, workPackage.Id);
        var body = await getRequest()!.Content!.ReadAsStringAsync();
        Assert.Contains("\"customField3\":{\"href\":\"/api/v3/custom_options/7\"}", body);
        Assert.Contains("\"customField5\":{\"href\":\"/api/v3/custom_options/11\"}", body);
    }

    [Fact]
    public async Task Execute_WithParentId_ShouldEmitParentLink()
    {
        // Una línea fácil de perder en un merge, y sin ella la subtarea se crea suelta:
        // OpenProject no falla, simplemente la deja como raíz.
        var (command, getRequest) = BuildCommand("""{ "id": 432, "subject": "Acta de firma" }""");

        var workPackage = await command.Execute(new CreateWorkPackageRequest(
            Subject: "Acta de firma", ProjectId: 4, ParentId: 412));

        Assert.Equal(432, workPackage.Id);
        var body = await getRequest()!.Content!.ReadAsStringAsync();
        Assert.Contains("\"parent\":{\"href\":\"/api/v3/work_packages/412\"}", body);
    }

    [Fact]
    public async Task Execute_SinParentId_NoDebeMandarElLink()
    {
        var (command, getRequest) = BuildCommand("""{ "id": 433, "subject": "Tarea suelta" }""");

        await command.Execute(new CreateWorkPackageRequest(Subject: "Tarea suelta", ProjectId: 4));

        var body = await getRequest()!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain("\"parent\"", body);
    }

    [Fact]
    public async Task Execute_CuandoOpenProjectRechaza_PropagaSuMotivo()
    {
        // El 422 de una jerarquía inválida trae el motivo redactado por OpenProject. Es lo que
        // el bot le muestra al usuario: el JSON crudo no le sirve a nadie.
        const string errorJson = """
            {
                "_type": "Error",
                "errorIdentifier": "urn:openproject-org:api:v3:errors:PropertyConstraintViolation",
                "message": "El padre no es válido porque pertenece a otro proyecto."
            }
            """;
        var (command, _) = BuildCommand(errorJson, HttpStatusCode.UnprocessableEntity);

        var ex = await Assert.ThrowsAsync<Exception>(() => command.Execute(
            new CreateWorkPackageRequest(Subject: "Acta", ProjectId: 4, ParentId: 412)));

        Assert.Equal("El padre no es válido porque pertenece a otro proyecto.", ex.Message);
    }
}
