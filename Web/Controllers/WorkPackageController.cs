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
    /// <summary>
    /// Una página de tareas. El estado y la búsqueda se filtran en OpenProject: traer las
    /// ~200 tareas para mostrar 12 costaba ~9 s, porque OpenProject cobra por cada work
    /// package que serializa.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedWorkPackages<WorkPackage>>> GetAllWorkPackages(
        [FromQuery] int? projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? search = null,
        [FromQuery] string? statusIds = null)
    {
        var ids = (statusIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

        var request = new ListsWorkPackagesRequest(
            projectId, page, pageSize, StatusIds: ids, Search: search);

        return await listCommand.ExecutePageAsync(request);
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