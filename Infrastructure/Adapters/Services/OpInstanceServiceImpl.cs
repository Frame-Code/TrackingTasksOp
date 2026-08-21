using Application.Dto.OpInstance;
using Application.Ports.Repositories;
using Application.Ports.Services;

namespace Infrastructure.Adapters.Services;

public class OpInstanceServiceImpl(IOpInstanceRepository repository) : IOpInstanceService
{
    public async Task save(OpInstanceDto dto) => await repository.Save(dto);
}