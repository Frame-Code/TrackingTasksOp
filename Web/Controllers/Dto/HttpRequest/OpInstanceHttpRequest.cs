namespace Web.Controllers.Dto.HttpRequest;

public record OpInstanceHttpRequest(
    string Alias,
    string ClientId,
    string ClientSecret
    );