using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public class MonthlyReportBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonthlyReportBackgroundService> _logger;

        public MonthlyReportBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MonthlyReportBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📊 [MONTHLY REPORT] MonthlyReportBackgroundService starting...");
            _logger.LogInformation("   Runs on the 1st of each month at 9:00 AM to generate and send monthly reports");

            // Calculate time until next month's 1st at 9:00 AM
            var now = DateTime.UtcNow;
            var nextMonth = now.AddMonths(1);
            var nextReportDate = new DateTime(nextMonth.Year, nextMonth.Month, 1, 9, 0, 0);
            
            // If we're past the 1st of current month at 9 AM, schedule for next month
            var currentMonthReportDate = new DateTime(now.Year, now.Month, 1, 9, 0, 0);
            if (now > currentMonthReportDate)
            {
                // Wait until next month
                var delay = nextReportDate - now;
                _logger.LogInformation($"⏰ [MONTHLY REPORT] Next report scheduled for {nextReportDate:yyyy-MM-dd HH:mm} UTC (in {delay.TotalDays:F1} days)");
                await Task.Delay(delay, stoppingToken);
            }
            else
            {
                // Wait until 1st of current month at 9 AM
                var delay = currentMonthReportDate - now;
                _logger.LogInformation($"⏰ [MONTHLY REPORT] Next report scheduled for {currentMonthReportDate:yyyy-MM-dd HH:mm} UTC (in {delay.TotalHours:F1} hours)");
                await Task.Delay(delay, stoppingToken);
            }

            // Run monthly report generation
            await GenerateAndSendMonthlyReportAsync(now.AddMonths(-1));

            // Then run every month
            var monthlyTimer = new PeriodicTimer(TimeSpan.FromDays(30));
            
            try
            {
                while (await monthlyTimer.WaitForNextTickAsync(stoppingToken))
                {
                    var reportMonth = DateTime.UtcNow.AddMonths(-1);
                    await GenerateAndSendMonthlyReportAsync(reportMonth);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("📊 [MONTHLY REPORT] Service stopping...");
            }
        }

        private async Task GenerateAndSendMonthlyReportAsync(DateTime reportMonth)
        {
            try
            {
                _logger.LogInformation($"📊 [MONTHLY REPORT] Generating report for {reportMonth:MMMM yyyy}");
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var reportService = scope.ServiceProvider.GetRequiredService<IMonthlyReportService>();
                    var success = await reportService.SendMonthlyReportToAdminsAsync(reportMonth);
                    
                    if (success)
                    {
                        _logger.LogInformation($"✅ [MONTHLY REPORT] Report for {reportMonth:MMMM yyyy} generated and sent successfully");
                    }
                    else
                    {
                        _logger.LogWarning($"⚠️ [MONTHLY REPORT] Report generation completed with warnings for {reportMonth:MMMM yyyy}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [MONTHLY REPORT] Error generating report for {reportMonth:MMMM yyyy}: {ex.Message}");
            }
        }
    }
}

