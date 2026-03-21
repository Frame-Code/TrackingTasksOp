using Application.Dto.ListWorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class WorkPackageController(
    IListsWorkPackagesCommand command) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkPackage>>> GetAllWorkPackages(
        [FromQuery]int? projectId, 
        [FromQuery] int offset, 
        [FromQuery] int pageSize)
    {
        var request = new ListsWorkPackagesRequest(projectId, offset, pageSize);
        return await command.Execute(request);
    }
}