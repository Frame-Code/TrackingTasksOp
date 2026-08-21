using Application.Dto.OpInstance;

namespace Application.Ports.Repositories;

public interface IOpInstanceRepository 
{
    Task Save(OpInstanceDto dto);
}