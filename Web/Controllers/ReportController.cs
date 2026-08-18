using Application.Dto.Reports;
using Application.Ports.UseCases.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportController(IGenerateDailyTaskReportCommand generateDailyTaskReportCommand) : ControllerBase
{
    [HttpGet("daily-tasks")]
    public async Task<IActionResult> DailyTasks([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? statusId)
    {
        var bytes = await generateDailyTaskReportCommand.Execute(from, to, statusId);
        var fileName = $"Reporte_Tareas_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Mismos datos del Excel pero en JSON, para la vista previa antes de imprimir o descargar.</summary>
    [HttpGet("daily-tasks/preview")]
    public async Task<ActionResult<DailyTaskReportData>> DailyTasksPreview(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? statusId)
        => await generateDailyTaskReportCommand.Build(from, to, statusId);
}
