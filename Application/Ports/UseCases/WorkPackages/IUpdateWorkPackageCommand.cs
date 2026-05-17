namespace Application.Ports.UseCases.WorkPackages;

public interface IUpdateWorkPackageCommand
{
    /// <summary>
    /// Actualiza un work package en OpenProject.
    /// Para startDate y dueDate: pasar null = borrar la fecha, omitir (default) = no tocar.
    /// El sentinel "__no_change__" es el valor por defecto que indica "no incluir el campo en el payload".
    /// </summary>
    Task Execute(
        int     workPackageId,
        int?    statusId        = null,
        int?    assigneeId      = null,
        int?    responsibleId   = null,
        int?    percentageDone  = null,
        string? startDate       = "__no_change__",
        string? dueDate         = "__no_change__");
}
