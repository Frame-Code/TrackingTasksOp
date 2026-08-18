namespace Application.Dto.Reports;

/// <summary>Una fila del reporte: horas registradas en OpenProject para un día/tarea/actividad.</summary>
public record DailyTaskReportRow(
    DateOnly Date,
    string ProjectName,
    int WorkPackageId,
    string TaskName,
    string ActivityName,
    double Hours,
    string Assignee = "",
    string Responsible = "",
    string Status = "",
    string Type = "");

/// <summary>Sesión cerrada en local que todavía no se registró en OpenProject.</summary>
public record PendingSessionRow(
    DateOnly Date,
    string ProjectName,
    int WorkPackageId,
    string TaskName,
    double Hours,
    string Assignee = "",
    string Responsible = "",
    string Status = "",
    string Type = "");

/// <summary>Contenido del reporte ya calculado, para renderizarlo como Excel o como vista previa.</summary>
public record DailyTaskReportData(List<DailyTaskReportRow> Rows, List<PendingSessionRow> Pending)
{
    public double TotalHours => Math.Round(Rows.Sum(r => r.Hours), 2);
}
