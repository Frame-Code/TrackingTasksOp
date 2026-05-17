namespace Application.Ports.UseCases.Tasks;

public interface ICancelTaskSessionCommand
{
    /// <summary>
    /// Elimina el último TaskTimeDetail sin EndTime de la tarea indicada.
    /// No sube nada a OpenProject.
    /// Retorna false si no había sesión activa para cancelar.
    /// </summary>
    Task<bool> Execute(int workPackageId);
}
