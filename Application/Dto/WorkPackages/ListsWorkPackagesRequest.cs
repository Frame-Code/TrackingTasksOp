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
        bool OnlyOpen = false
    ){}
}
