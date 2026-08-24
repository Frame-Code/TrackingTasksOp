namespace Application.Dto.OpInstance;

public record ListsOpInstanceDto (
    int Id,
    string BaseUrl,
    string Alias,
    bool IsOAuthConnected
    )
{
}
