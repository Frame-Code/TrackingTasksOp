using Application.Dto.OpInstance;

namespace Application.Ports.Services;

public interface IOpInstanceService
{
    Task save(OpInstanceDto dto);
}