using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    /// <summary>
    /// Background service that automatically adds overdue penalties (10 pesos per minute) 
    /// for books that are past their due date.
    /// </summary>
    public class OverdueProcessorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OverdueProcessorService> _logger;
        private Timer _timer;

        public OverdueProcessorService(IServiceProvider serviceProvider, ILogger<OverdueProcessorService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OverdueProcessorService starting...");

            // Start the timer immediately, then run every 30 seconds for real-time penalty updates
            _timer = new Timer(async _ => await ProcessOverdueBooks(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessOverdueBooks()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var mongoDbService = scope.ServiceProvider.GetRequiredService<IMongoDbService>();
                    var penaltyService = scope.ServiceProvider.GetRequiredService<IPenaltyService>();

                    var reservations = mongoDbService.GetCollection<Reservation>("Reservations");
                    var penalties = mongoDbService.GetCollection<Penalty>("Penalties");
                    var users = mongoDbService.GetCollection<User>("Users");
                    var studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");

                    // Find all reservations that should be tracked for overdue charging.
                    // Only "Borrowed" status is tracked - overdue penalties start when the student picks up the book,
                    // not when it's approved by the librarian/admin.
                    var trackedReservations = await reservations
                        .Find(r => r.Status == "Borrowed")
                        .ToListAsync();

                    _logger.LogInformation($"[OverdueProcessor] Found {trackedReservations.Count} tracked reservations to evaluate for overdue penalties");

                    decimal penaltyPerMinute = 10m; // 10 pesos per minute for testing

                    // Only consider reservations that have a due date and are past due
                    var overdueReservations = trackedReservations
                        .Where(r => r.DueDate.HasValue && r.DueDate.Value < DateTime.UtcNow)
                        .ToList();

                    _logger.LogInformation($"[OverdueProcessor] Found {overdueReservations.Count} overdue reservations (past due date)");

                    foreach (var reservation in overdueReservations)
                    {
                        try
                        {

                            // Calculate minutes overdue using DueDate
                            // DueDate is set when the book is marked as "Borrowed" (picked up), not when approved
                            if (!reservation.DueDate.HasValue)
                            {
                                continue;
                            }

                            var minutesOverdue = (int)(DateTime.UtcNow - reservation.DueDate.Value).TotalMinutes;

                            // Only create/update penalties when at least 1 minute has passed since due date
                            if (minutesOverdue <= 0) continue;

                            _logger.LogInformation($"[OverdueProcessor] Processing reservation {reservation._id}: {minutesOverdue} minute(s) overdue");

                            // Parse reservation UserId and _id
                            ObjectId reservationObjectId = ObjectId.Parse(reservation._id);
                            ObjectId userObjectId = ObjectId.Parse(reservation.UserId);

                            // Check if a penalty for this reservation already exists (to avoid duplicates)
                            var existingPenalty = await penalties
                                .Find(p => p.ReservationId == reservationObjectId &&
                                          p.PenaltyType == "Overdue")
                                .FirstOrDefaultAsync();

                            // Calculate penalty amount: 10 pesos per minute since due date (for testing)
                            decimal newPenaltyAmount = minutesOverdue * penaltyPerMinute;

                            if (existingPenalty != null)
                            {
                                // Update existing overdue penalty with recalculated amount
                                var update = Builders<Penalty>.Update
                                    .Set(p => p.Amount, newPenaltyAmount)
                                    .Set(p => p.Description, $"{minutesOverdue} min(s) = ₱{newPenaltyAmount}");

                                await penalties.UpdateOneAsync(p => p._id == existingPenalty._id, update);
                                _logger.LogInformation($"[OverdueProcessor] Updated overdue penalty for reservation {reservation._id}: ₱{newPenaltyAmount}");
                            }
                            else
                            {
                                // Create new overdue penalty
                                var user = await users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                                var studentProfile = await studentProfiles
                                    .Find(sp => sp.UserId == userObjectId)
                                    .FirstOrDefaultAsync();

                                if (user == null || studentProfile == null)
                                {
                                    _logger.LogWarning($"[OverdueProcessor] User or student profile not found for reservation {reservation._id}");
                                    continue;
                                }

                                var overduepenalty = new Penalty
                                {
                                    _id = ObjectId.GenerateNewId(),
                                    UserId = userObjectId,
                                    StudentNumber = studentProfile.StudentNumber,
                                    StudentName = user.FullName,
                                    BookTitle = reservation.BookTitle,
                                    ReservationId = reservationObjectId,
                                    PenaltyType = "Overdue",
                                    Amount = newPenaltyAmount,
                                    Description = $"{minutesOverdue} min(s) = ₱{newPenaltyAmount}",
                                    IsPaid = false,
                                    CreatedDate = DateTime.UtcNow,
                                    Remarks = $"{minutesOverdue} minute(s) overdue = ₱{newPenaltyAmount}"
                                };

                                await penalties.InsertOneAsync(overduepenalty);
                                _logger.LogInformation($"[OverdueProcessor] Created overdue penalty for reservation {reservation._id}: ₱{newPenaltyAmount}");

                                // Notify student about the penalty
                                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                                await notificationService.CreatePenaltyNotificationAsync(
                                    userObjectId.ToString(),
                                    reservation.BookTitle ?? "Unknown Book",
                                    newPenaltyAmount,
                                    "Overdue"
                                );
                                _logger.LogInformation($"[OverdueProcessor] Sent penalty notification to user {userObjectId}");
                            }

                            // Update user penalty status
                            await penaltyService.UpdateUserPenaltyStatusAsync(userObjectId.ToString());
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"[OverdueProcessor] Error processing reservation {reservation._id}: {ex.Message}");
                        }
                    }

                    _logger.LogInformation($"[OverdueProcessor] Completed processing overdue books");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[OverdueProcessor] Error in ProcessOverdueBooks: {ex.Message}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OverdueProcessorService stopping...");
            _timer?.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}
