using System.Text.Json;
using Infrastructure.Adapters.Services.Bot;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqToolsTests
{
    [Fact]
    public void All_ShouldSerializeStartTaskToolWithRequiredFields()
    {
        var json = JsonSerializer.Serialize(GroqTools.All);
        using var doc = JsonDocument.Parse(json);
        var tool = doc.RootElement[0];

        Assert.Equal("function", tool.GetProperty("type").GetString());

        var function = tool.GetProperty("function");
        Assert.Equal("start_task", function.GetProperty("name").GetString());

        var properties = function.GetProperty("parameters").GetProperty("properties");
        Assert.True(properties.TryGetProperty("projectName", out _));
        Assert.True(properties.TryGetProperty("name", out _));
        Assert.True(properties.TryGetProperty("customFields", out _));

        var required = function.GetProperty("parameters").GetProperty("required")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("name", required);
        Assert.Contains("projectName", required);
    }
}
