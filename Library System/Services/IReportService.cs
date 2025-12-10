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

        Task<ReportViewModel> GetCompleteReportAsync(string timeRange, DateTime? from = null, DateTime? to = null);
        Task<BorrowingTrendReport> GetBorrowingTrendAsync(string timeRange, DateTime? from = null, DateTime? to = null);
        Task<List<OverdueAccountReport>> GetOverdueAccountsAsync(DateTime? from = null, DateTime? to = null);
        Task<InventoryStatusReport> GetInventoryStatusAsync();
        Task<List<StudentActivityReport>> GetStudentActivityAsync(string timeRange, DateTime? from = null, DateTime? to = null);
        Task<object> GetDetailedBorrowingDataAsync(string type, int daysRange = 30);
        Task<object> GetDetailedInventoryDataAsync(string type, int daysRange = 30);
        Task<object> GetDetailedOverdueDataAsync(string type, int daysRange = 30);
    }
}