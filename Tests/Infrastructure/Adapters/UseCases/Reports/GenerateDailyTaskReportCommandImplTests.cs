using ClosedXML.Excel;
using Domain.Entities.OpenProjectEntities;
using Domain.Entities.OpenProjectEntities.TimeEntries;
using Application.Dto.Reports;
using Infrastructure.Adapters.UseCases.Reports;

namespace Tests.Infrastructure.Adapters.UseCases.Reports;

public class GenerateDailyTaskReportCommandImplTests
{
    private static OpTimeEntry Entry(string spentOn, string hours, int wpId, string wpTitle, string project, string activity) => new()
    {
        SpentOn = spentOn,
        Hours = hours,
        Links = new TimeEntryLinks
        {
            WorkPackage = new LinkObject { Href = $"/api/v3/work_packages/{wpId}", Title = wpTitle },
            Project = new LinkObject { Href = "/api/v3/projects/5", Title = project },
            Activity = new LinkObject { Href = "/api/v3/time_entries/activities/1", Title = activity }
        }
    };

    [Fact]
    public void BuildReportRows_AgrupaPorDiaTareaYActividad()
    {
        var entries = new List<OpTimeEntry>
        {
            Entry("2026-03-09", "PT1H30M", 1134, "Login", "eProduction", "Development"),
            Entry("2026-03-09", "PT30M",   1134, "Login", "eProduction", "Development"),
            Entry("2026-03-10", "PT2H",    1134, "Login", "eProduction", "Development")
        };

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(entries);

        Assert.Equal(2, rows.Count);
        Assert.Equal(2.0, rows[0].Hours);   // 1.5 + 0.5 el día 9
        Assert.Equal(2.0, rows[1].Hours);   // el día 10
    }

    [Fact]
    public void BuildReportRows_OrdenaPorFecha()
    {
        var entries = new List<OpTimeEntry>
        {
            Entry("2026-03-10", "PT1H", 2, "B", "P", "Development"),
            Entry("2026-01-05", "PT1H", 1, "A", "P", "Development")
        };

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(entries);

        Assert.Equal(new DateOnly(2026, 1, 5), rows[0].Date);
        Assert.Equal(new DateOnly(2026, 3, 10), rows[1].Date);
    }

    [Fact]
    public void BuildReportRows_NoInventaHoras_SumaExactamenteLoDeOpenProject()
    {
        var entries = new List<OpTimeEntry>
        {
            Entry("2026-03-09", "PT30M", 1134, "Login", "eProduction", "Development")
        };

        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(entries);

        Assert.Equal(0.5, Assert.Single(rows).Hours);
    }

    [Fact]
    public void BuildWorkbook_EscribeAmbasHojas()
    {
        var rows = GenerateDailyTaskReportCommandImpl.BuildReportRows(
            [Entry("2026-03-09", "PT1H", 1134, "Login", "eProduction", "Development")]);
        var pending = new List<PendingSessionRow> { new(new DateOnly(2026, 3, 9), "eProduction", 990, "Reporte", 0.75) };

        var bytes = GenerateDailyTaskReportCommandImpl.BuildWorkbook(rows, pending);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        Assert.True(wb.Worksheets.Contains("Reporte"));
        Assert.True(wb.Worksheets.Contains("Pendientes de subir"));
    }
}
