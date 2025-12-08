using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public class DuplicateAccountDetectionBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DuplicateAccountDetectionBackgroundService> _logger;

        public DuplicateAccountDetectionBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DuplicateAccountDetectionBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔍 [DUPLICATE DETECTION] DuplicateAccountDetectionBackgroundService starting...");
            _logger.LogInformation("   Runs daily at midnight to detect duplicate accounts and staff-as-student conflicts");

            // Run immediately on startup, then daily at midnight
            await RunDetectionAsync();

            // Calculate time until next midnight
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delayUntilMidnight = nextMidnight - now;

            // Wait until midnight
            await Task.Delay(delayUntilMidnight, stoppingToken);

            // Then run every 24 hours
            var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunDetectionAsync();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🔍 [DUPLICATE DETECTION] Service stopping...");
            }
        }

        private async Task RunDetectionAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var detectionService = scope.ServiceProvider.GetRequiredService<IDuplicateAccountDetectionService>();
                    var conflictsFound = await detectionService.RunDetectionAsync();
                    _logger.LogInformation($"✅ [DUPLICATE DETECTION] Detection complete. Found {conflictsFound} conflicts.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ [DUPLICATE DETECTION] Error during detection: {ex.Message}");
            }
        }
    }
}

