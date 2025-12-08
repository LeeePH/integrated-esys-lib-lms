using MongoDB.Driver;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using System.Text;

namespace SystemLibrary.Services
{
    public class MonthlyReportService : IMonthlyReportService
    {
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<ReturnTransaction> _returns;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Penalty> _penalties;
        private readonly IReportService _reportService;
        private readonly IEmailService _emailService;
        private readonly string _reportsDirectory;

        public MonthlyReportService(
            IMongoDbService mongoDbService,
            IReportService reportService,
            IEmailService emailService)
        {
            _books = mongoDbService.GetCollection<Book>("Books");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _returns = mongoDbService.GetCollection<ReturnTransaction>("Returns");
            _users = mongoDbService.GetCollection<User>("Users");
            _penalties = mongoDbService.GetCollection<Penalty>("Penalties");
            _reportService = reportService;
            _emailService = emailService;
            _reportsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reports", "monthly");
            
            // Ensure directory exists
            if (!Directory.Exists(_reportsDirectory))
            {
                Directory.CreateDirectory(_reportsDirectory);
            }
        }

        public async Task<string> GenerateMonthlyReportAsync(DateTime reportMonth)
        {
            try
            {
                var startDate = new DateTime(reportMonth.Year, reportMonth.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1).AddHours(23).AddMinutes(59).AddSeconds(59);

                Console.WriteLine($"📊 [MONTHLY REPORT] Generating report for {reportMonth:MMMM yyyy} ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})");

                // Get all data for the month
                var borrowingReport = await _reportService.GetBorrowingReportAsync((endDate - startDate).Days + 1);
                var overdueReport = await _reportService.GetOverdueReportAsync();
                var inventoryReport = await _reportService.GetInventoryReportAsync();

                // Get monthly statistics
                var totalBorrowings = await _reservations.CountDocumentsAsync(r =>
                    r.ApprovalDate >= startDate && r.ApprovalDate <= endDate);

                var totalReturns = await _returns.CountDocumentsAsync(r =>
                    r.CreatedAt >= startDate && r.CreatedAt <= endDate);

                var totalPenalties = await _penalties.CountDocumentsAsync(p =>
                    p.CreatedDate >= startDate && p.CreatedDate <= endDate);

                var totalPenaltyAmount = (await _penalties.Find(p =>
                    p.CreatedDate >= startDate && p.CreatedDate <= endDate)
                    .ToListAsync()).Sum(p => p.Amount);

                var activeUsers = await _users.CountDocumentsAsync(u => u.IsActive);
                var totalBooks = await _books.CountDocumentsAsync(_ => true);
                var availableBooks = await _books.CountDocumentsAsync(b => b.AvailableCopies > 0);

                // Generate HTML report
                var htmlReport = GenerateHtmlReport(
                    reportMonth,
                    startDate,
                    endDate,
                    totalBorrowings,
                    totalReturns,
                    totalPenalties,
                    totalPenaltyAmount,
                    activeUsers,
                    totalBooks,
                    availableBooks,
                    borrowingReport,
                    overdueReport,
                    inventoryReport
                );

                // Save HTML report
                var fileName = $"Monthly_Report_{reportMonth:yyyy_MM}.html";
                var filePath = Path.Combine(_reportsDirectory, fileName);
                await File.WriteAllTextAsync(filePath, htmlReport, Encoding.UTF8);

                Console.WriteLine($"✅ [MONTHLY REPORT] Report generated: {filePath}");

                // Generate PDF (using HTML to PDF conversion approach)
                // Note: For production, consider using QuestPDF or iTextSharp
                var pdfPath = await GeneratePdfFromHtmlAsync(filePath, reportMonth);
                
                return pdfPath ?? filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MONTHLY REPORT] Error generating report: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> SendMonthlyReportToAdminsAsync(DateTime reportMonth)
        {
            try
            {
                var reportPath = await GenerateMonthlyReportAsync(reportMonth);
                var adminUsers = await _users.Find(u => u.Role == "admin").ToListAsync();

                foreach (var admin in adminUsers)
                {
                    if (!string.IsNullOrEmpty(admin.Email))
                    {
                        await _emailService.SendEmailAsync(
                            admin.Email,
                            $"Monthly Library Report - {reportMonth:MMMM yyyy}",
                            $"Please find attached the monthly library report for {reportMonth:MMMM yyyy}.",
                            reportPath
                        );
                    }
                }

                Console.WriteLine($"✅ [MONTHLY REPORT] Report sent to {adminUsers.Count} admin(s)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MONTHLY REPORT] Error sending report: {ex.Message}");
                return false;
            }
        }

        private string GenerateHtmlReport(
            DateTime reportMonth,
            DateTime startDate,
            DateTime endDate,
            long totalBorrowings,
            long totalReturns,
            long totalPenalties,
            decimal totalPenaltyAmount,
            long activeUsers,
            long totalBooks,
            long availableBooks,
            BorrowingReportViewModel borrowingReport,
            OverdueReportViewModel overdueReport,
            InventoryReportViewModel inventoryReport)
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<title>Monthly Library Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("h1 { color: #2c3e50; }");
            html.AppendLine("h2 { color: #34495e; border-bottom: 2px solid #3498db; padding-bottom: 5px; }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            html.AppendLine("th { background-color: #3498db; color: white; }");
            html.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
            html.AppendLine(".summary-box { background-color: #ecf0f1; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine($"<h1>Monthly Library Report - {reportMonth:MMMM yyyy}</h1>");
            html.AppendLine($"<p><strong>Report Period:</strong> {startDate:MMMM dd, yyyy} to {endDate:MMMM dd, yyyy}</p>");
            html.AppendLine($"<p><strong>Generated:</strong> {DateTime.UtcNow:MMMM dd, yyyy HH:mm:ss} UTC</p>");

            // Summary Section
            html.AppendLine("<div class='summary-box'>");
            html.AppendLine("<h2>Summary Statistics</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Metric</th><th>Value</th></tr>");
            html.AppendLine($"<tr><td>Total Borrowings</td><td>{totalBorrowings}</td></tr>");
            html.AppendLine($"<tr><td>Total Returns</td><td>{totalReturns}</td></tr>");
            html.AppendLine($"<tr><td>Total Penalties Issued</td><td>{totalPenalties}</td></tr>");
            html.AppendLine($"<tr><td>Total Penalty Amount</td><td>₱{totalPenaltyAmount:N2}</td></tr>");
            html.AppendLine($"<tr><td>Active Users</td><td>{activeUsers}</td></tr>");
            html.AppendLine($"<tr><td>Total Books</td><td>{totalBooks}</td></tr>");
            html.AppendLine($"<tr><td>Available Books</td><td>{availableBooks}</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Borrowing Report Section
            html.AppendLine("<h2>Borrowing Statistics</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Month</th><th>Borrowed</th><th>Returned</th></tr>");
            foreach (var data in borrowingReport.MonthlyData.Take(30))
            {
                html.AppendLine($"<tr><td>{data.Month} {data.Year}</td><td>{data.TotalBorrowed}</td><td>{data.TotalReturned}</td></tr>");
            }
            html.AppendLine("</table>");

            // Overdue Report Section
            html.AppendLine("<h2>Overdue Accounts</h2>");
            html.AppendLine($"<p><strong>Total Overdue Accounts:</strong> {overdueReport.TotalOverdueAccounts}</p>");
            html.AppendLine($"<p><strong>Total Overdue Books:</strong> {overdueReport.TotalOverdueBooks}</p>");
            html.AppendLine($"<p><strong>Total Late Fees:</strong> ₱{overdueReport.TotalFees:N2}</p>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private async Task<string?> GeneratePdfFromHtmlAsync(string htmlPath, DateTime reportMonth)
        {
            try
            {
                // For now, return the HTML path
                // In production, use a library like QuestPDF, iTextSharp, or PuppeteerSharp
                // Example with QuestPDF would be:
                // var pdfPath = htmlPath.Replace(".html", ".pdf");
                // QuestPDF generation code here
                // return pdfPath;

                Console.WriteLine($"📄 [MONTHLY REPORT] PDF generation not yet implemented. HTML report available at: {htmlPath}");
                Console.WriteLine($"   💡 Consider adding QuestPDF or iTextSharp package for PDF generation.");
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [MONTHLY REPORT] Error generating PDF: {ex.Message}");
                return null;
            }
        }
    }
}

