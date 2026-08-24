using Application.Dto.OpInstance;

namespace Application.Ports.Services;

public interface IOpInstanceService
{
    Task Save(SaveOpInstanceDto dto);
    Task<IEnumerable<ListsOpInstanceDto>> Lists();
    Task<GetOpInstance?> GetOpInstance(int instanceId);
}