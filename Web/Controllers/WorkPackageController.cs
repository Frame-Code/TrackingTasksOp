using Application.Dto.ListWorkPackages;
using Application.Dto.WorkPackages;
using Application.Ports.UseCases.WorkPackages;
using Domain.Entities.OpenProjectEntities.WorkPackage;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class WorkPackageController(
    IListsWorkPackagesCommand listCommand,
    IUpdateWorkPackageCommand updateCommand) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WorkPackage>>> GetAllWorkPackages(
        [FromQuery] int? projectId,
        [FromQuery] int offset,
        [FromQuery] int pageSize)
    {
        var request = new ListsWorkPackagesRequest(projectId, offset, pageSize);
        return await listCommand.Execute(request);
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateWorkPackageRequest request)
    {
        await updateCommand.Execute(id, statusId: request.StatusId);
        return NoContent();
    }
}