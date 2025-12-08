using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    public interface IReservationService
    {
        // Existing methods
        Task<ReservationCreationResult> CreateReservationAsync(string userId, string bookId);
        Task<List<Reservation>> GetUserReservationsAsync(string userId);
        Task<List<Reservation>> GetPendingReservationsAsync();
        Task<List<Reservation>> GetActiveReservationsAsync();
        Task<Reservation> GetReservationByIdAsync(string reservationId);
        Task<bool> ApproveReservationAsync(string reservationId, string librarianId);
        Task<bool> RejectReservationAsync(string reservationId, string librarianId);
        Task<bool> CancelReservationAsync(string reservationId, string? cancelledByUserId = null, string? reason = null);
        Task<bool> MarkAsBorrowedAsync(string reservationId, string librarianId);
    // Test helper: Force set due date and optionally status (e.g., "Borrowed")
        Task<bool> ForceSetDueDateAsync(string reservationId, DateTime dueDate, string status = null, string librarianId = null);

        Task<BorrowingsViewModel> GetUserBorrowingsAsync(string userId);
        Task<List<Reservation>> GetUserBorrowedBooksAsync(string userId);
        Task<List<Reservation>> GetUserWaitlistAsync(string userId);
        Task<bool> RemoveFromWaitlistAsync(string reservationId, string userId);
        Task<bool> RequestRenewalAsync(string reservationId, string userId = null);
        Task<RenewalValidationResult> ValidateRenewalAsync(string reservationId, string userId = null);
        Task<bool> ApproveRenewalAsync(string reservationId, string librarianId);
        Task<bool> RejectRenewalAsync(string reservationId, string librarianId);
        Task<List<Reservation>> GetRenewalRequestsAsync();
        Task<List<Reservation>> GetAllReservationsInRangeAsync(DateTime startDate, DateTime endDate);
        Task<List<ReturnTransaction>> GetReturnsInRangeAsync(DateTime startDate, DateTime endDate);
        Task<Reservation?> GetActiveReservationByBookIdAsync(string bookId);
        Task<List<Reservation>> GetAllBorrowingHistoryAsync();
        Task<List<Reservation>> GetReturnedBooksAsync();
        Task<List<Reservation>> GetAllBorrowingsAsync();
        Task<int> AutoCancelExpiredPickupsAsync();
        Task<int> AutoRejectExpiredApprovalsAsync();
        Task<bool> ProcessNextInQueueAsync(string bookId);
        // Approve the next pending reservation AND hold one available copy (decrement available copies).
        // This is used when a copy becomes available (e.g., on return) to reserve it for the next student.
        Task<bool> ApproveNextAndHoldAsync(string bookId);
        Task<int> SendPickupRemindersAsync();

    }
}