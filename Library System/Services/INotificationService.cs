using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface INotificationService
    {
        // Core notification methods
        Task<bool> CreateNotificationAsync(Notification notification);
        Task<List<Notification>> GetUserNotificationsAsync(string userId, int limit = 50);
        Task<List<Notification>> GetUnreadNotificationsAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
        Task<Notification?> GetNotificationByIdAsync(string notificationId);

        // Additional retrieval methods
        Task<List<Notification>> GetArchivedNotificationsAsync(string userId);
        Task<List<Notification>> GetAllUserNotificationsAsync(string userId, int limit = 50);

        // Notification actions
        Task<bool> MarkAsReadAsync(string notificationId);
        Task<bool> MarkAllAsReadAsync(string userId);
        Task<bool> ArchiveNotificationAsync(string notificationId, bool isArchived);
        Task<bool> DeleteNotificationAsync(string notificationId);
        Task<bool> DeleteAllArchivedNotificationsAsync(string userId);

        // ✅ Added in the new version
        Task<bool> ClearAllReadNotificationsAsync(string userId);

        // Notification creation helpers
        Task CreateReservationCreatedNotificationAsync(string userId, string bookTitle, string reservationId);
        Task CreateReservationApprovedNotificationAsync(string userId, string bookTitle, DateTime dueDate);
        Task CreateReservationRejectedNotificationAsync(string userId, string bookTitle);
        Task CreateBookDueSoonNotificationAsync(string userId, string bookTitle, DateTime dueDate);
        Task CreateBookOverdueNotificationAsync(string userId, string bookTitle, int minutesOverdue);
        Task CreateRenewalRequestedNotificationAsync(string userId, string bookTitle);
        Task CreateBookReturnedNotificationAsync(string userId, string bookTitle, decimal penalty, string bookCondition, int daysLate);
        Task CreateBookBorrowedNotificationAsync(string userId, string bookTitle, DateTime dueDate);
        Task CreateReservationExpiredNotificationAsync(string userId, string bookTitle);
        Task CreatePickupReminderNotificationAsync(string userId, string bookTitle, DateTime pickupDeadline);
        Task CreatePenaltyNotificationAsync(string userId, string bookTitle, decimal amount, string penaltyType);
        Task CreatePenaltyPaidNotificationAsync(string userId, string bookTitle, decimal amount);
        Task CreateSuspiciousReservationAlertAsync(string userId, string studentName, string studentNumber, string bookTitle, int attemptsWithinWindow, int windowSeconds);
        Task CreateLibrarianReservationNotificationAsync(string studentName, string studentNumber, string bookTitle, string reservationId);
    }
}
