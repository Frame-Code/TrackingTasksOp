namespace Application.Dto.OpInstance;

public record SaveOpInstanceDto(
    int idInstance,
    string Alias,
    string ClientId,
    string ClientSecret
    )
{
    
}