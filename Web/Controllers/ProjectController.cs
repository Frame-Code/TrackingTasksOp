using Application.Dto.Projects;
using Application.Ports.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProjectController(IProjectOpService projectOpService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetProjects()
    {
        var projects = await projectOpService.Lists();
        return projects.Select(p => new ProjectDto(p.Id, p.Name)).ToList();
    }
}
