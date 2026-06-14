using Application.Dto.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;

namespace Application.Ports.UseCases.WorkPackages;

public interface ICreateWorkPackageCommand
{
    Task<WorkPackage> Execute(CreateWorkPackageRequest request);

    /// <summary>
    /// Consulta el formulario de creación de work packages de OpenProject para el proyecto y tipo dados,
    /// y devuelve los campos personalizados de tipo "CustomOption" marcados como obligatorios,
    /// junto con sus valores permitidos.
    /// </summary>
    Task<List<RequiredCustomField>> GetRequiredCustomFieldsAsync(int projectId, int? typeId = null);
}
