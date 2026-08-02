using System.ComponentModel.DataAnnotations;
using Application.Ports.Auth;
using Application.Ports.Repositories;
using Application.Ports.UseCases.Reports;
using ClosedXML.Excel;
using Task = Domain.Entities.TrackingTasksEntities.Task;

namespace Infrastructure.Adapters.UseCases.Reports;

internal record DailyTaskReportRow(DateOnly Date, string ProjectName, int WorkPackageId, string TaskName, string StatusName, double Hours);

public class GenerateDailyTaskReportCommandImpl(
    ITaskRepository taskRepository,
    IProjectRepository projectRepository,
    IStatusTaskRepository statusTaskRepository,
    CurrentUser currentUser) : IGenerateDailyTaskReportCommand
{
    public async System.Threading.Tasks.Task<byte[]> Execute(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new ValidationException("La fecha 'from' no puede ser posterior a 'to'.");

        var userId = currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDateExclusive = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var tasks = (await taskRepository.GetAllAsync(t =>
            t.UserId == userId &&
            t.TasksTimeDetails.Any(d => d.EndTime != null && d.StartTime >= fromDate && d.StartTime < toDateExclusive)))
            .ToList();

        var instanceId = currentUser.OpenProjectInstanceId;
        var projectNames = (await projectRepository.GetAllAsync(p => p.OpenProjectInstanceId == instanceId))
            .ToDictionary(p => p.Id, p => p.Name);
        var statusNames = (await statusTaskRepository.GetAllAsync(s => s.OpenProjectInstanceId == instanceId))
            .ToDictionary(s => s.Id, s => s.Name);

        var rows = BuildReportRows(tasks, fromDate, toDateExclusive, projectNames, statusNames);
        return BuildWorkbook(rows);
    }

    internal static List<DailyTaskReportRow> BuildReportRows(
        IEnumerable<Task> tasks,
        DateTime fromDate,
        DateTime toDateExclusive,
        IReadOnlyDictionary<int, string> projectNames,
        IReadOnlyDictionary<int, string> statusNames)
    {
        var rows = new List<DailyTaskReportRow>();

        foreach (var task in tasks)
        {
            var sessionsInRange = task.TasksTimeDetails
                .Where(d => d.EndTime != null && d.StartTime >= fromDate && d.StartTime < toDateExclusive);

            var byDay = sessionsInRange.GroupBy(d => d.StartTime.Date);

            foreach (var dayGroup in byDay)
            {
                var hours = Math.Round(dayGroup.Sum(d => (d.EndTime!.Value - d.StartTime).TotalHours), 2);
                if (hours <= 0) continue;

                rows.Add(new DailyTaskReportRow(
                    DateOnly.FromDateTime(dayGroup.Key),
                    projectNames.GetValueOrDefault(task.ProjectId, "Desconocido"),
                    task.WorkPackageId,
                    task.Name,
                    statusNames.GetValueOrDefault(task.StatusTaskId, "Desconocido"),
                    hours));
            }
        }

        return rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.ProjectName)
            .ThenBy(r => r.TaskName)
            .ToList();
    }

    internal static byte[] BuildWorkbook(List<DailyTaskReportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reporte");

        string[] headers = ["Fecha", "Proyecto", "ID Tarea", "Nombre", "Estado", "Horas"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var r in rows)
        {
            ws.Cell(row, 1).Value = r.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 2).Value = r.ProjectName;
            ws.Cell(row, 3).Value = r.WorkPackageId;
            ws.Cell(row, 4).Value = r.TaskName;
            ws.Cell(row, 5).Value = r.StatusName;
            ws.Cell(row, 6).Value = r.Hours;
            row++;
        }

        ws.Cell(row, 5).Value = "Total";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = rows.Sum(r => r.Hours);
        ws.Cell(row, 6).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
