using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SystemLibrary.Services
{
    public class ReservationQueueProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ReservationQueueProcessorService> _logger;
        private Timer? _timer;

        public ReservationQueueProcessorService(IServiceProvider serviceProvider, ILogger<ReservationQueueProcessorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("🚀 [QUEUE PROCESSOR] ReservationQueueProcessorService STARTED");
            Console.WriteLine("   Runs every 60 seconds to:");
            Console.WriteLine("   • Auto-cancel approved reservations after 2 minutes (if not picked up)");
            Console.WriteLine("   • Send pickup reminders to students");
            Console.WriteLine("   • Advance queue when slots open up");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");

            // Run immediately, then every 30 seconds (for faster auto-cancel detection)
            _timer = new Timer(async _ => await ProcessQueue(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessQueue()
        {
            try
            {
                Console.WriteLine("\n⏰ [QUEUE PROCESSOR] ─────────────────────────────────────────────────");
                Console.WriteLine($"⏰ [QUEUE PROCESSOR] Queue processing cycle started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                
                using (var scope = _serviceProvider.CreateScope())
                {
                    var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();

                    // 1) Auto-cancel reservations not picked up within 2 minutes
                    Console.WriteLine("⏳ [QUEUE PROCESSOR] → Phase 1: Checking for expired pickups (2m+ old)...");
                    var cancelled = await reservationService.AutoCancelExpiredPickupsAsync();

                    // 2) Send pickup reminders (optional)
                    Console.WriteLine("⏳ [QUEUE PROCESSOR] → Phase 2: Sending pickup reminders to approved students...");
                    var reminders = await reservationService.SendPickupRemindersAsync();
                    
                    Console.WriteLine($"✅ [QUEUE PROCESSOR] Cycle complete: {cancelled} auto-cancelled, {reminders} reminders sent");
                }
                
                Console.WriteLine("⏰ [QUEUE PROCESSOR] ─────────────────────────────────────────────────\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [QUEUE PROCESSOR] ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("🛑 [QUEUE PROCESSOR] ReservationQueueProcessorService STOPPING");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            _timer?.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}
