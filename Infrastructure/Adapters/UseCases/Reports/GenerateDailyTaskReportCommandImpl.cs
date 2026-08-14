using System.ComponentModel.DataAnnotations;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Reports;
using ClosedXML.Excel;
using Domain.Entities.OpenProjectEntities.TimeEntries;

namespace Infrastructure.Adapters.UseCases.Reports;

public record DailyTaskReportRow(DateOnly Date, string ProjectName, int WorkPackageId, string TaskName, string ActivityName, double Hours);

/// <summary>Sesión cerrada en local que todavía no se registró en OpenProject.</summary>
public record PendingSessionRow(DateOnly Date, string ProjectName, int WorkPackageId, string TaskName, double Hours);

/// <summary>
/// El reporte se construye a partir de las entradas de tiempo de OpenProject, que es la
/// fuente de verdad: incluye lo cargado a mano fuera de la app y funciona para cualquier
/// mes, sin depender de que la tarea siga existiendo en la BD local.
/// La segunda hoja muestra lo que quedó pendiente de subir, para que no pase desapercibido.
/// </summary>
public class GenerateDailyTaskReportCommandImpl(
    ITimeEntryOpService timeEntryOpService,
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    CurrentUser currentUser) : IGenerateDailyTaskReportCommand
{
    public async System.Threading.Tasks.Task<byte[]> Execute(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new ValidationException("La fecha 'from' no puede ser posterior a 'to'.");

        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var entries = await timeEntryOpService.Lists(from, to, currentUser.OpenProjectUserId);
        var rows = BuildReportRows(entries);
        var pending = await BuildPendingRows(userId, from, to);

        return BuildWorkbook(rows, pending);
    }

    internal static List<DailyTaskReportRow> BuildReportRows(IEnumerable<OpTimeEntry> entries)
    {
        return entries
            .Where(e => e.SpentOnDate != DateOnly.MinValue && e.HoursAsDouble > 0)
            .GroupBy(e => (e.SpentOnDate, e.WorkPackageId, e.ActivityTitle))
            .Select(g => new DailyTaskReportRow(
                g.Key.SpentOnDate,
                g.First().ProjectTitle,
                g.Key.WorkPackageId,
                g.First().WorkPackageTitle,
                g.Key.ActivityTitle,
                Math.Round(g.Sum(e => e.HoursAsDouble), 2)))
            .OrderBy(r => r.Date)
            .ThenBy(r => r.ProjectName)
            .ThenBy(r => r.TaskName)
            .ToList();
    }

    private async System.Threading.Tasks.Task<List<PendingSessionRow>> BuildPendingRows(string userId, DateOnly from, DateOnly to)
    {
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDateExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var tasks = (await taskRepository.GetAllAsync(t =>
            t.UserId == userId &&
            t.TasksTimeDetails.Any(d => !d.Uploaded && d.EndTime != null
                                        && d.StartTime >= fromDate && d.StartTime < toDateExclusive)))
            .ToList();

        var instanceId = currentUser.OpenProjectInstanceId;
        var projectNames = (await projectRepository.GetAllAsync(p => p.OpenProjectInstanceId == instanceId))
            .ToDictionary(p => p.Id, p => p.Name);

        return tasks
            .SelectMany(t => t.TasksTimeDetails
                .Where(d => !d.Uploaded && d.EndTime != null
                            && d.StartTime >= fromDate && d.StartTime < toDateExclusive)
                .GroupBy(d => d.StartTime.Date)
                .Select(g => new PendingSessionRow(
                    DateOnly.FromDateTime(g.Key),
                    projectNames.GetValueOrDefault(t.ProjectId, "Desconocido"),
                    t.WorkPackageId,
                    t.Name,
                    Math.Round(g.Sum(d => (d.EndTime!.Value - d.StartTime).TotalHours), 2))))
            .Where(r => r.Hours > 0)
            .OrderBy(r => r.Date)
            .ToList();
    }

    internal static byte[] BuildWorkbook(List<DailyTaskReportRow> rows, List<PendingSessionRow> pending)
    {
        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Reporte");
        WriteHeaders(ws, ["Fecha", "Proyecto", "ID Tarea", "Nombre", "Actividad", "Horas"]);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = r.ProjectName;
            ws.Cell(row, 3).Value = r.WorkPackageId;
            ws.Cell(row, 4).Value = r.TaskName;
            ws.Cell(row, 5).Value = r.ActivityName;
            ws.Cell(row, 6).Value = r.Hours;
            row++;
        }

        ws.Cell(row, 5).Value = "Total";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = Math.Round(rows.Sum(r => r.Hours), 2);
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        var wsPending = workbook.Worksheets.Add("Pendientes de subir");
        WriteHeaders(wsPending, ["Fecha", "Proyecto", "ID Tarea", "Nombre", "Horas sin registrar"]);

        var pendingRow = 2;
        foreach (var pr in pending)
        {
            wsPending.Cell(pendingRow, 1).Value = pr.Date.ToString("yyyy-MM-dd");
            wsPending.Cell(pendingRow, 2).Value = pr.ProjectName;
            wsPending.Cell(pendingRow, 3).Value = pr.WorkPackageId;
            wsPending.Cell(pendingRow, 4).Value = pr.TaskName;
            wsPending.Cell(pendingRow, 5).Value = pr.Hours;
            pendingRow++;
        }
        wsPending.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteHeaders(IXLWorksheet ws, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }
    }
}
