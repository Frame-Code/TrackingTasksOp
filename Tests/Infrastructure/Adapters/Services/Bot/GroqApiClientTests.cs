using Infrastructure.Adapters.Services.Bot;

namespace Tests.Infrastructure.Adapters.Services.Bot;

public class GroqApiClientTests
{
    [Fact]
    public void BuildSystemPrompt_ShouldRequireAskingStatusAndPercentageBeforeEndingTask()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("end_task_session", prompt);
        Assert.Contains("estado quiere cambiar la tarea", prompt);
        Assert.Contains("percentageDone", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldResolveSelfAssignmentToYo()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("asígnamela a mí", prompt);
        Assert.Contains("\"yo\"", prompt);
        Assert.Contains("assigneeName", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldForbidSayingActionsArePending()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("CONFIRMACIÓN DE ACCIONES EJECUTADAS", prompt);
        Assert.Contains("no se ha creado", prompt);
    }
}
