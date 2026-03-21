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
        int pageSize
    ){}
}
