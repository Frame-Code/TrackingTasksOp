using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dto.ListWorkPackages
{
    public record ListsWorkPackagesRequest (
        int? ProjectId,
        int offset,
        int pageSize,
        int? StatusId = null,
        /// <summary>Solo tareas abiertas. El listado general trae todos los estados
        /// (incluidas las cerradas); el bot, cuando habla de "pendientes", no debe.</summary>
        bool OnlyOpen = false,
        /// <summary>Varios estados a la vez (las pildoras de la UI). Tiene prioridad sobre StatusId.</summary>
        IReadOnlyCollection<int>? StatusIds = null,
        /// <summary>Texto libre; OpenProject lo busca dentro del asunto de la tarea.</summary>
        string? Search = null
    ){}

    /// <summary>Una pagina de resultados junto al total, para paginar sin traerlo todo.</summary>
    public record PagedWorkPackages<T>(List<T> Items, int Total, int Page, int PageSize);
}
