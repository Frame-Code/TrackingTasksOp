using Application.Dto.OpInstance;
using Application.Ports.Repositories;
using Application.Ports.Services;

namespace Infrastructure.Adapters.Services;

public class OpInstanceServiceImpl(IOpInstanceRepository repository) : IOpInstanceService
{
    public async Task<IEnumerable<ListsOpInstanceDto>> Lists() => await repository.Lists();
    public async Task<GetOpInstance?> GetOpInstance(int instanceId) => await repository.GetOpInstance(instanceId);
    public async Task Save(SaveOpInstanceDto dto) => await repository.Save(dto);
}