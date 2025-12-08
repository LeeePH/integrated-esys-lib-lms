using MongoDB.Driver;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IMongoCollection<Notification> _notifications;
        private readonly IMongoDbService _mongoDbService;

        public NotificationService(IMongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
            _notifications = mongoDbService.GetCollection<Notification>("Notifications");
        }

        public async Task<bool> CreateNotificationAsync(Notification notification)
        {
            try
            {
                await _notifications.InsertOneAsync(notification);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // UPDATED: Filter out archived notifications by default
        public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 50)
        {
            return await _notifications
                .Find(n => n.UserId == userId && !n.IsArchived)
                .SortByDescending(n => n.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync(string userId)
        {
            return await _notifications
                .Find(n => n.UserId == userId && !n.IsRead && !n.IsArchived)
                .SortByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return (int)await _notifications
                .CountDocumentsAsync(n => n.UserId == userId && !n.IsRead && !n.IsArchived);
        }

        // NEW: Get archived notifications
        public async Task<List<Notification>> GetArchivedNotificationsAsync(string userId)
        {
            return await _notifications
                .Find(n => n.UserId == userId && n.IsArchived)
                .SortByDescending(n => n.ArchivedAt)
                .ToListAsync();
        }

        // NEW: Get all notifications including archived
        public async Task<List<Notification>> GetAllUserNotificationsAsync(string userId, int limit = 50)
        {
            return await _notifications
                .Find(n => n.UserId == userId)
                .SortByDescending(n => n.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<Notification?> GetNotificationByIdAsync(string notificationId)
        {
            try
            {
                return await _notifications
                    .Find(n => n._id == notificationId)
                    .FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> ClearAllReadNotificationsAsync(string userId)
        {
            try
            {
                var filter = Builders<Notification>.Filter.Where(n =>
                    n.UserId == userId && n.IsRead && !n.IsArchived);

                var update = Builders<Notification>.Update
                    .Set(n => n.IsArchived, true)
                    .Set(n => n.ArchivedAt, DateTime.UtcNow);

                var result = await _notifications.UpdateManyAsync(filter, update);

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }


        public async Task<bool> MarkAsReadAsync(string notificationId)
        {
            try
            {
                var update = Builders<Notification>.Update
                    .Set(n => n.IsRead, true)
                    .Set(n => n.ReadAt, DateTime.UtcNow);

                var result = await _notifications.UpdateOneAsync(
                    n => n._id == notificationId,
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var update = Builders<Notification>.Update
                    .Set(n => n.IsRead, true)
                    .Set(n => n.ReadAt, DateTime.UtcNow);

                var result = await _notifications.UpdateManyAsync(
                    n => n.UserId == userId && !n.IsRead && !n.IsArchived,
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ArchiveNotificationAsync(string notificationId, bool isArchived)
        {
            try
            {
                var update = Builders<Notification>.Update
                    .Set(n => n.IsArchived, isArchived)
                    .Set(n => n.ArchivedAt, isArchived ? DateTime.UtcNow : (DateTime?)null);

                var result = await _notifications.UpdateOneAsync(
                    n => n._id == notificationId,
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteNotificationAsync(string notificationId)
        {
            try
            {
                var result = await _notifications.DeleteOneAsync(n => n._id == notificationId);
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteAllArchivedNotificationsAsync(string userId)
        {
            try
            {
                var result = await _notifications.DeleteManyAsync(
                    n => n.UserId == userId && n.IsArchived
                );
                return result.DeletedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        // Helper methods
        public async Task CreateReservationCreatedNotificationAsync(string userId, string bookTitle, string reservationId)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "RESERVATION_CREATED",
                Title = "RESERVATION CONFIRMED!",
                Message = $"Your reservation for '{bookTitle}' has been submitted and is pending approval.",
                BookTitle = bookTitle,
                ReservationId = reservationId
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateReservationApprovedNotificationAsync(string userId, string bookTitle, DateTime dueDate)
        {
            var phTime = ConvertToPhilippinesTime(dueDate);
            var notification = new Notification
            {
                UserId = userId,
                Type = "RESERVATION_APPROVED",
                Title = "RESERVATION APPROVED!",
                Message = $"Your reservation for '{bookTitle}' has been approved. Pick up by: {phTime:MMM dd, yyyy hh:mm tt}",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateReservationRejectedNotificationAsync(string userId, string bookTitle)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "RESERVATION_REJECTED",
                Title = "RESERVATION CANCELLED",
                Message = $"Your reservation for '{bookTitle}' has been cancelled by the librarian.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateBookDueSoonNotificationAsync(string userId, string bookTitle, DateTime dueDate)
        {
            var phTime = ConvertToPhilippinesTime(dueDate);
            var notification = new Notification
            {
                UserId = userId,
                Type = "BOOK_DUE_SOON",
                Title = "BOOK DUE SOON!",
                Message = $"Your borrowed book '{bookTitle}' is due on {phTime:MMM dd, yyyy hh:mm tt}. Please return it on time.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateBookOverdueNotificationAsync(string userId, string bookTitle, int minutesOverdue)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "BOOK_OVERDUE",
                Title = "BOOK OVERDUE!",
                Message = $"Your borrowed book '{bookTitle}' is {minutesOverdue} minute(s) overdue. Late fees apply (₱{minutesOverdue * 10}). Please return it immediately.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateRenewalRequestedNotificationAsync(string userId, string bookTitle)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "RENEWAL_REQUESTED",
                Title = "RENEWAL REQUESTED",
                Message = $"Your renewal request for '{bookTitle}' has been submitted and is pending approval.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateBookReturnedNotificationAsync(string userId, string bookTitle, decimal penalty, string bookCondition,
        int daysLate)
        {
            var message = penalty > 0
                ? $"Your book '{bookTitle}' has been returned. Total penalty: ₱{penalty:N2}. Please settle your payment."
                : $"Your book '{bookTitle}' has been returned successfully. No penalties.";

            var notification = new Notification
            {
                UserId = userId,
                Type = "BOOK_RETURNED",
                Title = "BOOK RETURNED",
                Message = message,
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateBookBorrowedNotificationAsync(string userId, string bookTitle, DateTime dueDate)
        {
            var phTime = ConvertToPhilippinesTime(dueDate);
            var notification = new Notification
            {
                UserId = userId,
                Type = "BOOK_BORROWED",
                Title = "BOOK BORROWED!",
                Message = $"You have successfully borrowed '{bookTitle}'. Due date: {phTime:MMM dd, yyyy hh:mm tt}.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateReservationExpiredNotificationAsync(string userId, string bookTitle)
        {
            var phTime = ConvertToPhilippinesTime(DateTime.UtcNow);
            var notification = new Notification
            {
                UserId = userId,
                Type = "RESERVATION_EXPIRED",
                Title = "RESERVATION EXPIRED",
                Message = $"Your reservation for '{bookTitle}' has expired. You did not pick up within 2 minutes. Expired at: {phTime:MMM dd, yyyy hh:mm tt}.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreatePickupReminderNotificationAsync(string userId, string bookTitle, DateTime pickupDeadline)
        {
            var phTime = ConvertToPhilippinesTime(pickupDeadline);
            var notification = new Notification
            {
                UserId = userId,
                Type = "PICKUP_REMINDER",
                Title = "PICKUP REMINDER",
                Message = $"⏰ URGENT: Your reserved book '{bookTitle}' must be picked up by {phTime:MMM dd, yyyy hh:mm tt}. Please claim it within 2 minutes!",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreatePenaltyNotificationAsync(string userId, string bookTitle, decimal amount, string penaltyType)
        {
            var phTime = ConvertToPhilippinesTime(DateTime.UtcNow);
            var notification = new Notification
            {
                UserId = userId,
                Type = "PENALTY_ISSUED",
                Title = "PENALTY ISSUED",
                Message = $"A penalty of ₱{amount:F2} has been issued for '{bookTitle}' due to {penaltyType.ToLower()} on {phTime:MMM dd, yyyy hh:mm tt}. You cannot borrow books until this penalty is settled.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreatePenaltyPaidNotificationAsync(string userId, string bookTitle, decimal amount)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "PENALTY_PAID",
                Title = "PENALTY PAID",
                Message = $"Your penalty of ₱{amount:F2} for '{bookTitle}' has been marked as paid. You can now borrow books again.",
                BookTitle = bookTitle
            };
            await CreateNotificationAsync(notification);
        }

        public async Task CreateSuspiciousReservationAlertAsync(
            string userId,
            string studentName,
            string studentNumber,
            string bookTitle,
            int attemptsWithinWindow,
            int windowSeconds)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = "SUSPICIOUS_RESERVATION",
                Title = "Suspicious Reservation Activity",
                Message = $"{studentName} ({studentNumber}) triggered {attemptsWithinWindow} reservations within {windowSeconds} seconds for '{bookTitle}'.",
                BookTitle = bookTitle,
                ReservationId = string.Empty
            };

            await CreateNotificationAsync(notification);
        }

        public async Task CreateLibrarianReservationNotificationAsync(string studentName, string studentNumber, string bookTitle, string reservationId)
        {
            // Get all librarians
            var usersCollection = _mongoDbService.GetCollection<User>("Users");
            var librarianFilter = Builders<User>.Filter.Eq(u => u.Role, "librarian");
            var librarians = await usersCollection.Find(librarianFilter).ToListAsync();

            // Create notification for each librarian
            foreach (var librarian in librarians)
            {
                var notification = new Notification
                {
                    UserId = librarian._id.ToString(),
                    Type = "NEW_RESERVATION",
                    Title = "NEW BOOK RESERVATION",
                    Message = $"{studentName} ({studentNumber}) has reserved '{bookTitle}'. Please review and approve.",
                    BookTitle = bookTitle,
                    ReservationId = reservationId
                };
                await CreateNotificationAsync(notification);
            }
        }

        // Helper: Convert UTC time to Philippines Time (UTC+8)
        private DateTime ConvertToPhilippinesTime(DateTime utcTime)
        {
            TimeZoneInfo phTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); // UTC+8
            return TimeZoneInfo.ConvertTime(utcTime, TimeZoneInfo.Utc, phTimeZone);
        }
    }
}

