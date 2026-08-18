using System.ComponentModel.DataAnnotations;
using Application.Dto.Reports;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.Services;
using Application.Ports.UseCases.Reports;
using Application.Ports.UseCases.WorkPackages;
using ClosedXML.Excel;
using Domain.Entities.OpenProjectEntities.TimeEntries;

namespace Infrastructure.Adapters.UseCases.Reports;

/// <summary>Datos del work package que no vienen en las entradas de tiempo.</summary>
public record WorkPackageInfo(string Assignee, string Responsible, int StatusId, string Status, string Type)
{
    public static readonly WorkPackageInfo Unknown = new("", "", 0, "", "");
}

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
    IListsWorkPackagesCommand listsWorkPackagesCommand,
    CurrentUser currentUser) : IGenerateDailyTaskReportCommand
{
    public async System.Threading.Tasks.Task<byte[]> Execute(DateOnly from, DateOnly to, int? statusId = null)
    {
        var data = await Build(from, to, statusId);
        return BuildWorkbook(data.Rows, data.Pending);
    }

    public async System.Threading.Tasks.Task<DailyTaskReportData> Build(DateOnly from, DateOnly to, int? statusId = null)
    {
        if (from > to)
            throw new ValidationException("La fecha 'from' no puede ser posterior a 'to'.");

        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var entries = await timeEntryOpService.Lists(from, to, currentUser.OpenProjectUserId);
        var rows = BuildReportRows(entries);
        var pending = await BuildPendingRows(userId, from, to);

        // Asignado, responsable, estado y tipo no vienen en las entradas de tiempo, así que se
        // resuelven consultando los work packages involucrados en una sola tanda.
        var info = await ResolveInfo(rows.Select(r => r.WorkPackageId)
            .Concat(pending.Select(p => p.WorkPackageId)));

        rows = rows.Select(r => Enrich(r, info.GetValueOrDefault(r.WorkPackageId, WorkPackageInfo.Unknown))).ToList();
        pending = pending.Select(p => Enrich(p, info.GetValueOrDefault(p.WorkPackageId, WorkPackageInfo.Unknown))).ToList();

        if (statusId is > 0)
        {
            // Se filtra por el estado ACTUAL del work package, que es lo que el usuario ve en la UI.
            rows = rows.Where(r => info.GetValueOrDefault(r.WorkPackageId, WorkPackageInfo.Unknown).StatusId == statusId).ToList();
            pending = pending.Where(p => info.GetValueOrDefault(p.WorkPackageId, WorkPackageInfo.Unknown).StatusId == statusId).ToList();
        }

        return new DailyTaskReportData(rows, pending);
    }

    private static DailyTaskReportRow Enrich(DailyTaskReportRow r, WorkPackageInfo i) =>
        r with { Assignee = i.Assignee, Responsible = i.Responsible, Status = i.Status, Type = i.Type };

    private static PendingSessionRow Enrich(PendingSessionRow p, WorkPackageInfo i) =>
        p with { Assignee = i.Assignee, Responsible = i.Responsible, Status = i.Status, Type = i.Type };

    private async System.Threading.Tasks.Task<Dictionary<int, WorkPackageInfo>> ResolveInfo(IEnumerable<int> workPackageIds)
    {
        var ids = workPackageIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return [];

        var workPackages = await listsWorkPackagesCommand.GetByIdsAsync(ids);

        return workPackages.ToDictionary(
            wp => wp.Id,
            wp => new WorkPackageInfo(
                wp.Links.Assignee.Title,
                wp.Links.Responsible.Title,
                ExtractId(wp.Links.Status.Href),
                wp.Links.Status.Title,
                wp.Links.Type.Title));
    }

    /// <summary>Último segmento numérico de un href tipo "/api/v3/statuses/7". 0 si no se puede leer.</summary>
    internal static int ExtractId(string? href) =>
        int.TryParse(href?.TrimEnd('/').Split('/').LastOrDefault(), out var id) ? id : 0;

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
        WriteHeaders(ws, ["Fecha", "Proyecto", "ID Tarea", "Tipo", "Nombre", "Estado", "Actividad", "Asignado", "Responsable", "Horas"]);

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = r.ProjectName;
            ws.Cell(row, 3).Value = r.WorkPackageId;
            ws.Cell(row, 4).Value = r.Type;
            ws.Cell(row, 5).Value = r.TaskName;
            ws.Cell(row, 6).Value = r.Status;
            ws.Cell(row, 7).Value = r.ActivityName;
            ws.Cell(row, 8).Value = r.Assignee;
            ws.Cell(row, 9).Value = r.Responsible;
            ws.Cell(row, 10).Value = r.Hours;
            row++;
        }

        ws.Cell(row, 9).Value = "Total";
        ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 10).Value = Math.Round(rows.Sum(r => r.Hours), 2);
        ws.Cell(row, 10).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();

        var wsPending = workbook.Worksheets.Add("Pendientes de subir");
        WriteHeaders(wsPending, ["Fecha", "Proyecto", "ID Tarea", "Tipo", "Nombre", "Estado", "Asignado", "Responsable", "Horas sin registrar"]);

        var pendingRow = 2;
        foreach (var pr in pending)
        {
            wsPending.Cell(pendingRow, 1).Value = pr.Date.ToString("yyyy-MM-dd");
            wsPending.Cell(pendingRow, 2).Value = pr.ProjectName;
            wsPending.Cell(pendingRow, 3).Value = pr.WorkPackageId;
            wsPending.Cell(pendingRow, 4).Value = pr.Type;
            wsPending.Cell(pendingRow, 5).Value = pr.TaskName;
            wsPending.Cell(pendingRow, 6).Value = pr.Status;
            wsPending.Cell(pendingRow, 7).Value = pr.Assignee;
            wsPending.Cell(pendingRow, 8).Value = pr.Responsible;
            wsPending.Cell(pendingRow, 9).Value = pr.Hours;
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
