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

    [Fact]
    public void BuildSystemPrompt_ShouldForbidSuccessTextWithoutJsonBlock()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("SOLO puede decir que una tarea/sesión/acción fue creada", prompt);
        Assert.Contains("el sistema NO ejecuta nada sin el JSON", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldRequireMinimalJsonWhenAnsweringMissingDataQuestion()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("RESPUESTA A DATOS FALTANTES", prompt);
        Assert.Contains("ÚNICAMENTE", prompt);
        Assert.Contains("NO hace falta que los repitas", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ShouldForbidAskingForIdsWhenNameWasGiven()
    {
        var prompt = GroqApiClient.BuildSystemPrompt();

        Assert.Contains("NUNCA le preguntes al usuario \"¿cuál es el ID?\"", prompt);
        Assert.Contains("EJEMPLO (regla 2 - nombres, NUNCA IDs)", prompt);
        Assert.Contains("\"responsibleName\": \"Juan Pérez\"", prompt);
    }
}
