using Application.Dto.ListWorkPackages;
using Domain.Entities.OpenProjectEntities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.UseCases.WorkPackages;

public interface IListsWorkPackagesCommand
{
    Task<List<WorkPackage>> Execute(ListsWorkPackagesRequest request);
}
