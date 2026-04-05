namespace Application.Ports.UseCases.WorkPackages;

public interface IUpdateWorkPackageCommand
{
    Task Execute(int workPackageId, int? statusId = null, int? assigneeId = null, int? responsibleId = null);
}
