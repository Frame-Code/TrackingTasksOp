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

    [HttpPatch("{id:int}/progress")]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateWorkPackageProgressRequest request)
    {
        await updateCommand.Execute(id, percentageDone: request.PercentageDone);
        return NoContent();
    }

    [HttpPatch("{id:int}/dates")]
    public async Task<IActionResult> UpdateDates(int id, [FromBody] UpdateWorkPackageDatesRequest request)
    {
        await updateCommand.Execute(id, startDate: request.StartDate, dueDate: request.DueDate);
        return NoContent();
    }
}