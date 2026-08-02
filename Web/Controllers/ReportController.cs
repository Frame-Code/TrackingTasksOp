using Application.Ports.UseCases.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReportController(IGenerateDailyTaskReportCommand generateDailyTaskReportCommand) : ControllerBase
{
    [HttpGet("daily-tasks")]
    public async Task<IActionResult> DailyTasks([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var bytes = await generateDailyTaskReportCommand.Execute(from, to);
        var fileName = $"Reporte_Tareas_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
