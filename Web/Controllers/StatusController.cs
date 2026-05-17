using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Status;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StatusController(IStatusOpService statusOpService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Status>>> GetStatuses()
    {
        var statuses = await statusOpService.Lists();
        return Ok(statuses);
    }
}
