using Application.Dto.Reports;

namespace Application.Ports.UseCases.Reports;

public interface IGenerateDailyTaskReportCommand
{
    /// <summary>Datos del reporte, para la vista previa en pantalla.</summary>
    Task<DailyTaskReportData> Build(DateOnly from, DateOnly to, int? statusId = null);

    /// <summary>El mismo reporte serializado como libro de Excel.</summary>
    Task<byte[]> Execute(DateOnly from, DateOnly to, int? statusId = null);
}
