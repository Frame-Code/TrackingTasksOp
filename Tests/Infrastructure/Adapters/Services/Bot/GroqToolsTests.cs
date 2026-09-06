using System.Text.Json;
using Infrastructure.Adapters.Services.Bot;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqToolsTests
{
    [Fact]
    public void All_ShouldSerializeCreateTaskToolWithRequiredFields()
    {
        var json = JsonSerializer.Serialize(GroqTools.All);
        using var doc = JsonDocument.Parse(json);
        var tool = doc.RootElement[0];

        Assert.Equal("function", tool.GetProperty("type").GetString());

        var function = tool.GetProperty("function");
        Assert.Equal("create_task", function.GetProperty("name").GetString());

        var properties = function.GetProperty("parameters").GetProperty("properties");
        Assert.True(properties.TryGetProperty("projectName", out _));
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("customFields", out _));

        var required = function.GetProperty("parameters").GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("name", required);

        // El proyecto NO es obligatorio en el schema: una subtarea lo hereda del padre, y
        // exigirlo hacía que el bot repreguntara un dato que ya puede deducir.
        Assert.DoesNotContain("projectName", required);
    }

    [Fact]
    public void CreateTask_DeclaraElPadreParaCrearSubtareas()
    {
        var json = JsonSerializer.Serialize(GroqTools.All);
        using var doc = JsonDocument.Parse(json);

        var properties = doc.RootElement[0].GetProperty("function")
            .GetProperty("parameters").GetProperty("properties");

        Assert.True(properties.TryGetProperty("parentId", out var parentId));
        Assert.Equal("integer", parentId.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("parentName", out _));
    }

    [Fact]
    public void All_DeclaraCreateTaskYStartTask()
    {
        var json = JsonSerializer.Serialize(GroqTools.All);

        Assert.Contains("\"create_task\"", json);
        Assert.Contains("\"start_task\"", json);
    }
}
