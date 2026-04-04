using Application.Ports.Services;
using Domain.Entities.OpenProjectEntities.Activity;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ActivityController(IActivityOpService activityOpService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ActivityAllowedValue>>> GetActivities([FromQuery] int workPackageId)
    {
        return await activityOpService.Lists(workPackageId);
    }
}
