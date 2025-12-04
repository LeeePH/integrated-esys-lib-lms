namespace SystemLibrary.Services
{
    public interface IMonthlyReportService
    {
        Task<string> GenerateMonthlyReportAsync(DateTime reportMonth);
        Task<bool> SendMonthlyReportToAdminsAsync(DateTime reportMonth);
    }
}

