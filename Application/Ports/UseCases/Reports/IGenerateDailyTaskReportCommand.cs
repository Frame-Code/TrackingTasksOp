namespace Application.Ports.UseCases.Reports;

public interface IGenerateDailyTaskReportCommand
{
    Task<byte[]> Execute(DateOnly from, DateOnly to);
}
