namespace Application.Dto.OpInstance;

public record OpInstanceDto(
    int idInstance,
    string Alias,
    string ClientId,
    string ClientSecret
    )
{
    
}