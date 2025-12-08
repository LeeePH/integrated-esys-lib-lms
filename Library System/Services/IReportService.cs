using SystemLibrary.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public interface IReportService
    {
        Task<BorrowingReportViewModel> GetBorrowingReportAsync(int daysRange = 30);
        Task<OverdueReportViewModel> GetOverdueReportAsync(int daysRange = 30);
        Task<InventoryReportViewModel> GetInventoryReportAsync(int daysRange = 30);

        Task<StudentActivityReportViewModel> GetStudentActivityReportAsync(int daysRange = 30);

        Task<ReportViewModel> GetCompleteReportAsync(string timeRange);
        Task<BorrowingTrendReport> GetBorrowingTrendAsync(string timeRange);
        Task<List<OverdueAccountReport>> GetOverdueAccountsAsync();
        Task<InventoryStatusReport> GetInventoryStatusAsync();
        Task<List<StudentActivityReport>> GetStudentActivityAsync(string timeRange);
        Task<object> GetDetailedBorrowingDataAsync(string type, int daysRange = 30);
        Task<object> GetDetailedInventoryDataAsync(string type, int daysRange = 30);
    }
}