using Application.Dto.OpInstance;

namespace Application.Ports.Repositories;

public interface IOpInstanceRepository 
{
    Task Save(SaveOpInstanceDto dto);
    Task<IEnumerable<ListsOpInstanceDto>> Lists();
    Task<GetOpInstance?> GetOpInstance(int instanceId);
}