using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SystemLibrary.Services
{
    /// <summary>
    /// Result of renewal validation check
    /// </summary>
    public class RenewalValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty; // "DueToday" or "HasPenalties"
    }

    public class ReservationCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ErrorCode { get; set; }
    }

    public class ReservationService : IReservationService
    {
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<ReturnTransaction> _returns;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly INotificationService _notificationService;
        private readonly IPenaltyService _penaltyService;
        private const int SuspiciousReservationWindowSeconds = 10;
        private const int SuspiciousReservationThreshold = 3;

        public ReservationService(IMongoDbService mongoDbService, INotificationService notificationService, IPenaltyService penaltyService)
        {
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _books = mongoDbService.GetCollection<Book>("Books");
            _returns = mongoDbService.GetCollection<ReturnTransaction>("Returns");
            _notificationService = notificationService;
            _penaltyService = penaltyService;
            _users = mongoDbService.GetCollection<User>("Users");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
        }

        private async Task HandleSuspiciousReservationAttemptAsync(
            User user,
            StudentProfile? studentProfile,
            Book book,
            int attemptsWithinWindow)
        {
            try
            {
                var staffRoles = new[] { "admin", "librarian" };
                var staffFilter = Builders<User>.Filter.In(u => u.Role, staffRoles);
                var staffUsers = await _users.Find(staffFilter).ToListAsync();
                var studentName = user.FullName ?? user.Username ?? "Unknown Student";
                var studentNumber = studentProfile?.StudentNumber ?? "N/A";

                foreach (var staff in staffUsers)
                {
                    await _notificationService.CreateSuspiciousReservationAlertAsync(
                        staff._id.ToString(),
                        studentName,
                        studentNumber,
                        book.Title ?? "Unknown Book",
                        attemptsWithinWindow,
                        SuspiciousReservationWindowSeconds
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HandleSuspiciousReservationAttemptAsync] Error sending alerts: {ex.Message}");
            }
        }

        private async Task ReleaseInventoryHoldAsync(Reservation reservation, Book? existingBook = null)
        {
            if (!reservation.InventoryHoldActive)
                return;

            var book = existingBook ?? await FindBookByIdOrIsbn(reservation.BookId);
            if (book == null)
                return;

            var bookUpdate = Builders<Book>.Update
                .Inc(b => b.AvailableCopies, 1)
                .Set(b => b.UpdatedAt, DateTime.UtcNow);

            await _books.UpdateOneAsync(b => b._id == book._id, bookUpdate);
        }

        public async Task<ReservationCreationResult> CreateReservationAsync(string userId, string bookId)
        {
            try
            {
                if (!ObjectId.TryParse(userId, out var userObjectId))
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] Invalid user ID format: {userId}");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "Invalid user account."
                    };
                }

                var user = await _users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                if (user == null)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] User {userId} not found");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "We could not find your account. Please sign in again."
                    };
                }

                if (user.IsRestricted)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] User {userId} is restricted");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "Your account is restricted. Please contact the librarian."
                    };
                }

                if (user.HasPendingPenalties)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] User {userId} has pending penalties flag set");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "You must settle your penalties before reserving books."
                    };
                }

                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                if (pendingPenalties.Any())
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] User {userId} has {pendingPenalties.Count} pending penalties, cannot create reservation");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "You must settle your penalties before reserving books."
                    };
                }

                var studentProfile = await _studentProfiles.Find(sp => sp.UserId == userObjectId).FirstOrDefaultAsync();

                var book = await FindBookByIdOrIsbn(bookId);
                if (book == null)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] Book {bookId} not found");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "The selected book was not found."
                    };
                }

                if (!book.IsActive)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] Book '{book.Title}' is not active");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "This book is not available for reservations at the moment."
                    };
                }

                if (book.IsReferenceOnly)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] Book '{book.Title}' is reference-only");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "Reference-only books cannot be reserved."
                    };
                }

                // Prevent multiple active/ongoing reservations for the same book by the same user
                // Allow if previous is already Returned, Cancelled, Rejected, or Lost/Damaged/Completed
                var activeStatuses = new[] { "Pending", "Approved", "Borrowed", "Overdue", "ReturnPending", "RenewalPending" };
                var existingActive = await _reservations
                    .Find(r => r.UserId == userId
                               && r.BookId == bookId
                               && activeStatuses.Contains(r.Status))
                    .FirstOrDefaultAsync();
                if (existingActive != null)
                {
                    Console.WriteLine($"❌ [CREATE RESERVATION] User {userId} already has an active/ongoing reservation for book {bookId}");
                    return new ReservationCreationResult
                    {
                        Success = false,
                        Message = "You already have a reservation or borrowing for this book."
                    };
                }

                var now = DateTime.UtcNow;
                var suspiciousWindowStart = now.AddSeconds(-SuspiciousReservationWindowSeconds);
                var suspiciousFilter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.UserId, userId),
                    Builders<Reservation>.Filter.Gte(r => r.ReservationDate, suspiciousWindowStart)
                );
                var recentReservedCount = await _reservations.CountDocumentsAsync(suspiciousFilter);

                if (recentReservedCount >= SuspiciousReservationThreshold - 1)
                {
                    var attemptCount = (int)Math.Min(int.MaxValue, recentReservedCount + 1);
                    await HandleSuspiciousReservationAttemptAsync(user, studentProfile, book, attemptCount);
                    Console.WriteLine($"❌ [CREATE RESERVATION] Suspicious activity detected for {userId}. Attempts within window: {attemptCount}");

                    var flaggedReservation = new Reservation
                    {
                        _id = ObjectId.GenerateNewId().ToString(),
                        UserId = userId,
                        BookId = bookId,
                        BookTitle = book.Title ?? "Unknown Book",
                        Status = "Flagged",
                        ReservationDate = now,
                        StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                        FullName = user.FullName ?? user.Username ?? "Unknown Student",
                        BorrowType = "ONLINE",
                        IsSuspicious = true,
                        SuspiciousReason = $"Detected {attemptCount} reservations within {SuspiciousReservationWindowSeconds} seconds.",
                        SuspiciousDetectedAt = now,
                        Notes = "Auto-flagged by security policy"
                    };

                    await _reservations.InsertOneAsync(flaggedReservation);

                    return new ReservationCreationResult
                    {
                        Success = false,
                        ErrorCode = "SUSPICIOUS_ACTIVITY",
                        Message = "We detected unusual activity. Please wait a moment before trying again."
                    };
                }

                var reservation = new Reservation
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    UserId = userId,
                    BookId = bookId,
                    BookTitle = book.Title ?? "Unknown Book",
                    Status = "Pending",
                    ReservationDate = now,
                    StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                    FullName = user.FullName ?? user.Username ?? "Unknown Student",
                    BorrowType = "ONLINE"
                };

                await _reservations.InsertOneAsync(reservation);

                Console.WriteLine($"✅ [CREATE RESERVATION] Pending reservation created for {user.FullName ?? "Unknown"}, Book: '{book.Title}', Available Copies: {book.AvailableCopies}");
                if (book.AvailableCopies <= 0)
                {
                    Console.WriteLine("   📊 Book unavailable. Student added to waitlist.");
                }

                await _notificationService.CreateReservationCreatedNotificationAsync(
                    userId,
                    book.Title,
                    reservation._id
                );

                // Notify all librarians about the new reservation
                var studentName = user.FullName ?? user.Username ?? "Unknown Student";
                var studentNumber = studentProfile?.StudentNumber ?? "N/A";
                
                await _notificationService.CreateLibrarianReservationNotificationAsync(
                    studentName,
                    studentNumber,
                    book.Title ?? "Unknown Book",
                    reservation._id.ToString()
                );

                return new ReservationCreationResult
                {
                    Success = true,
                    Message = "Book reservation submitted successfully!"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [CREATE RESERVATION] Exception: {ex.Message}");
                return new ReservationCreationResult
                {
                    Success = false,
                    Message = "An unexpected error occurred while creating your reservation."
                };
            }
        }

        public async Task<List<Reservation>> GetUserReservationsAsync(string userId)
        {
            return await _reservations.Find(r => r.UserId == userId)
                .SortByDescending(r => r.ReservationDate)
                .ToListAsync();
        }

        public async Task<List<Reservation>> GetPendingReservationsAsync()
        {
            return await _reservations.Find(r => r.Status == "Pending").ToListAsync();
        }

        public async Task<List<Reservation>> GetActiveReservationsAsync()
        {
            return await _reservations.Find(r => r.Status == "Borrowed").ToListAsync();
        }

        public async Task<Reservation> GetReservationByIdAsync(string reservationId)
        {
            return await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
        }

        public async Task<bool> ApproveReservationAsync(string reservationId, string librarianId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            // Use helper method to find book safely
            var book = await FindBookByIdOrIsbn(reservation.BookId);
            if (book == null)
                return false;

            if (!book.IsActive || book.IsReferenceOnly || book.AvailableCopies <= 0)
                return false;

            var updateReservation = Builders<Reservation>.Update
                .Set(r => r.Status, "Approved")
                .Set(r => r.ApprovedBy, librarianId)
                .Set(r => r.ApprovalDate, DateTime.UtcNow);
                // Note: DueDate is NOT set here - it will be set when the book is marked as "Borrowed" (picked up)

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, updateReservation);
            bool success = result.ModifiedCount > 0;

            if (success)
            {
                // CRITICAL: Do NOT decrease available copies here!
                // Copies are only decreased when status becomes "Borrowed"
                // This allows the 2-minute pickup window without locking up inventory

                await _notificationService.CreateReservationApprovedNotificationAsync(
                    reservation.UserId,
                    book.Title,
                    DateTime.UtcNow.AddDays(14)
                );
            }

            return success;
        }

        public async Task<bool> RejectReservationAsync(string reservationId, string librarianId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            var update = Builders<Reservation>.Update
                .Set(r => r.Status, "Rejected")
                .Set(r => r.RejectedBy, librarianId)
                .Set(r => r.ApprovalDate, DateTime.UtcNow);

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
            bool success = result.ModifiedCount > 0;

            if (success)
            {
                // Use helper method to find book safely
                var book = await FindBookByIdOrIsbn(reservation.BookId);
                if (book != null)
                {
                    await _notificationService.CreateReservationRejectedNotificationAsync(
                        reservation.UserId,
                        book.Title
                    );
                }
            }

            return success;
        }

        public async Task<bool> CancelReservationAsync(string reservationId, string? cancelledByUserId = null, string? reason = null)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            if (reservation.Status == "Borrowed")
                return false;

            var book = await FindBookByIdOrIsbn(reservation.BookId);

            await ReleaseInventoryHoldAsync(reservation, book);

            var update = Builders<Reservation>.Update
                .Set(r => r.Status, "Cancelled")
                .Set(r => r.InventoryHoldActive, false)
                .Set(r => r.Notes, reason ?? "Reservation cancelled");

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
            if (result.ModifiedCount == 0)
                return false;

            if (book != null && cancelledByUserId != reservation.UserId)
            {
                await _notificationService.CreateReservationRejectedNotificationAsync(
                    reservation.UserId,
                    book.Title
                );
            }

            await ApproveNextAndHoldAsync(reservation.BookId);
            return true;
        }

        // NEW METHOD: Mark reservation as "Borrowed" when student picks up the book
        public async Task<bool> MarkAsBorrowedAsync(string reservationId, string librarianId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            // Only allow transition from "Approved" to "Borrowed"
            if (reservation.Status != "Approved")
                return false;

            // Use helper method to find book safely
            var book = await FindBookByIdOrIsbn(reservation.BookId);
            if (book == null)
                return false;

            if (!book.IsActive || book.IsReferenceOnly || book.AvailableCopies <= 0)
                return false;

            var updateReservation = Builders<Reservation>.Update
                .Set(r => r.Status, "Borrowed")
                .Set(r => r.ApprovedBy, librarianId)
                .Set(r => r.InventoryHoldActive, false)
                .Set(r => r.DueDate, DateTime.UtcNow); // Due immediately for testing

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, updateReservation);
            bool success = result.ModifiedCount > 0;

            if (success)
            {
                if (!reservation.InventoryHoldActive)
                {
                    // Only decrease available copies if we didn't already hold one
                    var updateBook = Builders<Book>.Update
                        .Inc(b => b.AvailableCopies, -1);

                    await _books.UpdateOneAsync(b => b._id == book._id, updateBook);
                }

                await _notificationService.CreateBookBorrowedNotificationAsync(
                    reservation.UserId,
                    book.Title,
                    DateTime.UtcNow // Due immediately for testing
                );
            }

            return success;
        }

        // Test helper: Force set due date and optionally set status (e.g., "Borrowed")
        public async Task<bool> ForceSetDueDateAsync(string reservationId, DateTime dueDate, string status = null, string librarianId = null)
        {
            try
            {
                var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
                if (reservation == null)
                    return false;

                var update = Builders<Reservation>.Update
                    .Set(r => r.DueDate, dueDate)
                    .Set(r => r.ApprovalDate, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(status))
                {
                    update = update.Set(r => r.Status, status);
                }

                if (!string.IsNullOrEmpty(librarianId))
                {
                    update = update.Set(r => r.ApprovedBy, librarianId);
                }

                var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
                if (result.ModifiedCount == 0)
                    return false;

                // If we set status to Borrowed, decrease available copies and notify
                if (!string.IsNullOrEmpty(status) && status == "Borrowed")
                {
                    var book = await FindBookByIdOrIsbn(reservation.BookId);
                    if (book != null)
                    {
                        var updateBook = Builders<Book>.Update
                            .Inc(b => b.AvailableCopies, -1);

                        await _books.UpdateOneAsync(b => b._id == book._id, updateBook);

                        await _notificationService.CreateBookBorrowedNotificationAsync(
                            reservation.UserId,
                            book.Title,
                            dueDate
                        );
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReservationService] ForceSetDueDateAsync error: {ex.Message}");
                return false;
            }
        }

        // Approve next pending reservation AND decrement available copies to hold the returned book.
        // This attempts to reserve the book for the next student when a copy becomes available.
        public async Task<bool> ApproveNextAndHoldAsync(string bookId)
        {
            try
            {
                // Find the next pending reservation for the book ordered by reservation date
                var filter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.BookId, bookId),
                    Builders<Reservation>.Filter.Eq(r => r.Status, "Pending")
                );

                var nextReservation = await _reservations.Find(filter)
                    .SortBy(r => r.ReservationDate)
                    .FirstOrDefaultAsync();

                if (nextReservation == null)
                {
                    Console.WriteLine($"🛑 [AUTO-APPROVE] No pending reservations for book {bookId}");
                    return false;
                }

                // Resolve book ObjectId
                Book book = await FindBookByIdOrIsbn(bookId);
                if (book == null)
                {
                    Console.WriteLine($"🛑 [AUTO-APPROVE] Book not found for id {bookId}");
                    return false;
                }

                // Try to decrement available copies atomically (only if > 0)
                var bookFilter = Builders<Book>.Filter.And(
                    Builders<Book>.Filter.Eq(b => b._id, book._id),
                    Builders<Book>.Filter.Gt(b => b.AvailableCopies, 0)
                );

                var bookUpdate = Builders<Book>.Update
                    .Inc(b => b.AvailableCopies, -1)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                var bookResult = await _books.UpdateOneAsync(bookFilter, bookUpdate);
                if (bookResult.ModifiedCount == 0)
                {
                    Console.WriteLine($"🛑 [AUTO-APPROVE] No available copies to hold for book '{book.Title}' ({book._id})");
                    return false;
                }

                // Now mark reservation as Approved
                var update = Builders<Reservation>.Update
                    .Set(r => r.Status, "Approved")
                    .Set(r => r.ApprovalDate, DateTime.UtcNow)
                    .Set(r => r.PickupReminderSent, false)
                    .Set(r => r.InventoryHoldActive, true);
                    // Note: DueDate is NOT set here - it will be set when the book is marked as "Borrowed" (picked up)

                var resUpdateResult = await _reservations.UpdateOneAsync(r => r._id == nextReservation._id, update);
                if (resUpdateResult.ModifiedCount > 0)
                {
                    var bookTitle = book?.Title ?? "Unknown";
                    var studentNum = nextReservation.StudentNumber ?? "Unknown";
                    Console.WriteLine($"✅ [AUTO-APPROVE] Approved and held book '{bookTitle}' for student {studentNum}");

                    // Notify the student
                    await _notificationService.CreateReservationApprovedNotificationAsync(
                        nextReservation.UserId,
                        bookTitle,
                        DateTime.UtcNow.AddDays(7)
                    );

                    return true;
                }

                // If reservation update failed, rollback book decrement
                var rollback = Builders<Book>.Update.Inc(b => b.AvailableCopies, 1);
                await _books.UpdateOneAsync(b => b._id == book._id, rollback);
                Console.WriteLine($"❌ [AUTO-APPROVE] Failed to update reservation {nextReservation._id}, rolled back book hold");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [AUTO-APPROVE] Error: {ex.Message}");
                return false;
            }
        }

        public async Task<BorrowingsViewModel> GetUserBorrowingsAsync(string userId)
        {
            var borrowings = new BorrowingsViewModel
            {
                BorrowedBooks = await GetUserBorrowedBooksViewModelsAsync(userId),
                Waitlist = await GetUserWaitlistViewModelsAsync(userId)
            };

            return borrowings;
        }

        public async Task<List<Reservation>> GetUserBorrowedBooksAsync(string userId)
        {
            // Only return "Borrowed" status - books that the student has actually picked up
            // "Approved" books should not appear in the dashboard until they are marked as "Borrowed"
            return await _reservations.Find(r => r.UserId == userId && r.Status == "Borrowed").ToListAsync();
        }

        public async Task<List<Reservation>> GetUserWaitlistAsync(string userId)
        {
            return await _reservations.Find(r => r.UserId == userId && (r.Status == "Pending" || r.Status == "Approved")).ToListAsync();
        }

        private async Task<List<BorrowedBookViewModel>> GetUserBorrowedBooksViewModelsAsync(string userId)
        {
            var reservations = await GetUserBorrowedBooksAsync(userId);
            var borrowedBooks = new List<BorrowedBookViewModel>();

            foreach (var reservation in reservations)
            {
                // Use helper method to find book safely
                var book = await FindBookByIdOrIsbn(reservation.BookId);

                if (book != null)
                {
                    borrowedBooks.Add(new BorrowedBookViewModel
                    {
                        ReservationId = reservation._id,
                        BookId = reservation.BookId,
                        Title = book.Title ?? "Unknown Book",
                        Author = book.Author ?? "Unknown Author",
                        Image = !string.IsNullOrEmpty(book.Image)
                            ? book.Image
                            : "/images/default-book.png",
                        BorrowedDate = reservation.ApprovalDate ?? reservation.ReservationDate,
                        DueDate = reservation.DueDate ?? DateTime.UtcNow.AddDays(14),
                        Status = reservation.Status,
                        DaysRemaining = reservation.DaysRemaining
                    });
                }
                else
                {
                    borrowedBooks.Add(new BorrowedBookViewModel
                    {
                        ReservationId = reservation._id,
                        BookId = reservation.BookId,
                        Title = reservation.BookTitle ?? "Unknown Book",
                        Author = "Unknown Author",
                        Image = "/images/default-book.png",
                        BorrowedDate = reservation.ReservationDate,
                        DueDate = reservation.DueDate ?? DateTime.UtcNow.AddDays(14),
                        Status = reservation.Status,
                        DaysRemaining = reservation.DaysRemaining
                    });
                }
            }

            return borrowedBooks;
        }

        private async Task<List<WaitlistViewModel>> GetUserWaitlistViewModelsAsync(string userId)
        {
            var reservations = await GetUserWaitlistAsync(userId);
            var waitlist = new List<WaitlistViewModel>();

            foreach (var reservation in reservations)
            {
                // Use helper method to find book safely
                var book = await FindBookByIdOrIsbn(reservation.BookId);

                waitlist.Add(new WaitlistViewModel
                {
                    Id = reservation._id,
                    BookTitle = book?.Title ?? reservation.BookTitle ?? "Unknown Book",
                    BookAuthor = book?.Author ?? "Unknown Author",
                    ReservationType = "Reserved",
                    ReservationDate = reservation.ReservationDate,
                    Status = reservation.Status
                });
            }

            return waitlist;
        }

        public async Task<bool> RemoveFromWaitlistAsync(string reservationId, string userId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId && r.UserId == userId).FirstOrDefaultAsync();
            if (reservation == null)
                return false;

            var book = await FindBookByIdOrIsbn(reservation.BookId);
            await ReleaseInventoryHoldAsync(reservation, book);

            var update = Builders<Reservation>.Update
                .Set(r => r.Status, "Cancelled")
                .Set(r => r.InventoryHoldActive, false)
                .Set(r => r.Notes, "Cancelled by student");

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId && r.UserId == userId, update);
            if (result.ModifiedCount == 0)
                return false;

            await ApproveNextAndHoldAsync(reservation.BookId);
            return true;
        }

        public async Task<bool> RequestRenewalAsync(string reservationId, string userId = null)
        {
            try
            {
                var validationResult = await ValidateRenewalAsync(reservationId, userId);
                if (!validationResult.IsValid)
                {
                    Console.WriteLine($"[RequestRenewal] Validation failed: {validationResult.Message}");
                    return false;
                }

                // If all validations pass, proceed with renewal request
                var update = Builders<Reservation>.Update.Set(r => r.Status, "Renewal Requested");
                var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RequestRenewal] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates if a book can be renewed with specific reasons
        /// </summary>
        public async Task<RenewalValidationResult> ValidateRenewalAsync(string reservationId, string userId = null)
        {
            try
            {
                // Get the reservation
                var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
                if (reservation == null)
                {
                    return new RenewalValidationResult
                    {
                        IsValid = false,
                        Message = "Reservation not found.",
                        Reason = "NotFound"
                    };
                }

                // Use reservation's userId if not provided
                if (string.IsNullOrEmpty(userId))
                    userId = reservation.UserId;

                // Check if DaysRemaining is 0 days (cannot renew if 0 days left)
                if (reservation.DueDate.HasValue)
                {
                    int daysRemaining = (int)(reservation.DueDate.Value - DateTime.UtcNow).TotalDays;
                    if (daysRemaining <= 0)
                    {
                        return new RenewalValidationResult
                        {
                            IsValid = false,
                            Message = "This book is due today or is already overdue. Renewal is not allowed for books with no time remaining.",
                            Reason = "DueToday"
                        };
                    }
                }

                // Check if student has any unpaid penalties (Damage, Lost, or Overdue)
                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                var unpaidPenalties = penalties.Where(p => 
                    !p.IsPaid && 
                    (p.PenaltyType == "Damage" || p.PenaltyType == "Lost" || p.PenaltyType == "Late")
                ).ToList();

                if (unpaidPenalties.Any())
                {
                    var penaltyTypes = string.Join(", ", unpaidPenalties.Select(p => p.PenaltyType).Distinct());
                    return new RenewalValidationResult
                    {
                        IsValid = false,
                        Message = $"You have unpaid penalties ({penaltyTypes}). Please settle all penalties before requesting a renewal.",
                        Reason = "HasPenalties"
                    };
                }

                // All validations passed
                return new RenewalValidationResult
                {
                    IsValid = true,
                    Message = "You are eligible to request a renewal.",
                    Reason = "Valid"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ValidateRenewal] Error: {ex.Message}");
                return new RenewalValidationResult
                {
                    IsValid = false,
                    Message = "An error occurred while validating your renewal request.",
                    Reason = "Error"
                };
            }
        }

        public async Task<bool> ApproveRenewalAsync(string reservationId, string librarianId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null || reservation.Status != "Renewal Requested")
                return false;

            // Extend the due date by 14 days
            var newDueDate = DateTime.UtcNow.AddDays(14);
            var update = Builders<Reservation>.Update
                .Set(r => r.Status, "Borrowed")
                .Set(r => r.DueDate, newDueDate)
                .Set(r => r.Notes, "Renewal approved by librarian");

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> RejectRenewalAsync(string reservationId, string librarianId)
        {
            var reservation = await _reservations.Find(r => r._id == reservationId).FirstOrDefaultAsync();
            if (reservation == null || reservation.Status != "Renewal Requested")
                return false;

            var update = Builders<Reservation>.Update
                .Set(r => r.Status, "Borrowed")
                .Set(r => r.Notes, "Renewal request rejected by librarian");

            var result = await _reservations.UpdateOneAsync(r => r._id == reservationId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<List<Reservation>> GetRenewalRequestsAsync()
        {
            return await _reservations.Find(r => r.Status == "Renewal Requested").ToListAsync();
        }

        public async Task<List<Reservation>> GetAllReservationsInRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _reservations
                .Find(r => r.ReservationDate >= startDate && r.ReservationDate <= endDate)
                .ToListAsync();
        }

        public async Task<List<ReturnTransaction>> GetReturnsInRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _returns
                .Find(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate)
                .ToListAsync();
        }

        public async Task<Reservation?> GetActiveReservationByBookIdAsync(string bookId)
        {
            try
            {
                // FIRST: Try to find the book by ISBN (since user searches by ISBN/Accession)
                var bookFilter = Builders<Book>.Filter.Eq("isbn", bookId);
                var book = await _books.Find(bookFilter).FirstOrDefaultAsync();

                if (book == null)
                {
                    Console.WriteLine($"No book found with ISBN: {bookId}");
                    return null;
                }

                // Now search for active reservation using the book's ObjectId
                var reservationFilter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.BookId, book._id.ToString()),
                    Builders<Reservation>.Filter.In(r => r.Status, new[] { "Approved", "Borrowed" })
                );

                var reservation = await _reservations.Find(reservationFilter)
                    .SortByDescending(r => r.ApprovalDate)
                    .FirstOrDefaultAsync();

                return reservation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveReservationByBookIdAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // CRITICAL: Helper method to safely find books by ID or ISBN
        private async Task<Book> FindBookByIdOrIsbn(string identifier)
        {
            // Try as ObjectId first
            if (ObjectId.TryParse(identifier, out var objectId))
            {
                var objectIdFilter = Builders<Book>.Filter.Eq(b => b._id, objectId);
                var book = await _books.Find(objectIdFilter).FirstOrDefaultAsync();
                if (book != null) return book;
            }

            // Try as ISBN using string-based filter to avoid ObjectId parsing errors
            var isbnFilter = Builders<Book>.Filter.Eq("isbn", identifier);
            return await _books.Find(isbnFilter).FirstOrDefaultAsync();
        }
        public async Task<List<Reservation>> GetAllBorrowingHistoryAsync()
        {
            // Get both currently borrowed (Approved) AND returned books
            var reservations = await _reservations
                .Find(r => r.Status == "Approved" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost" || r.Status == "Borrowed")
                .ToListAsync();

            // Sort in C# after fetching, not in MongoDB query
            return reservations
                .OrderByDescending(r => r.ApprovalDate ?? r.ReservationDate)
                .ToList();
        }

        public async Task<List<Reservation>> GetReturnedBooksAsync()
        {
            // Get only returned books
            return await _reservations
                .Find(r => r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost")
                .SortByDescending(r => r.ReturnDate)
                .ToListAsync();
        }

        public async Task<List<Reservation>> GetAllBorrowingsAsync()
        {
            var filter = Builders<Reservation>.Filter.In(r => r.Status, new[] { "Approved", "Borrowed" });
            return await _reservations.Find(filter)
                .SortByDescending(r => r.ReservationDate)
                .ToListAsync();
        }
        // Add this method to your ReservationService class


        public async Task<int> AutoCancelExpiredPickupsAsync()
        {
            try
            {
                // Find all Approved reservations where 2 minutes have passed without pickup
                var now = DateTime.UtcNow;
                var expiredFilter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.Status, "Approved"),
                    Builders<Reservation>.Filter.Lt(r => r.ApprovalDate, now.AddMinutes(-2))
                );

                var expiredReservations = await _reservations.Find(expiredFilter).ToListAsync();
                Console.WriteLine($"⏱️  [QUEUE PROCESSOR] AutoCancelExpiredPickups: Found {expiredReservations.Count} expired Approved reservations (2m+ old)");

                int cancelledCount = 0;

                foreach (var reservation in expiredReservations)
                {
                    var book = await FindBookByIdOrIsbn(reservation.BookId);
                    var bookTitle = book?.Title ?? "Unknown";
                    var studentNum = reservation.StudentNumber ?? "Unknown";
                    
                    Console.WriteLine($"⏱️  [QUEUE PROCESSOR] Auto-cancelling unpicked book: Book='{bookTitle}', Student={studentNum}, ApprovedAt={reservation.ApprovalDate}");
                    
                    await ReleaseInventoryHoldAsync(reservation, book);

                    // Cancel the reservation
                    var update = Builders<Reservation>.Update
                        .Set(r => r.Status, "Cancelled")
                        .Set(r => r.InventoryHoldActive, false)
                        .Set(r => r.Notes, "Auto-cancelled: Student did not pick up within 2 minutes");

                    var result = await _reservations.UpdateOneAsync(r => r._id == reservation._id, update);

                    if (result.ModifiedCount > 0)
                    {
                        cancelledCount++;
                        Console.WriteLine($"✅ [QUEUE PROCESSOR] Reservation auto-cancelled: Book='{bookTitle}', Student={studentNum}");

                        // Send notification to student about expired reservation
                        if (book != null)
                        {
                            await _notificationService.CreateReservationExpiredNotificationAsync(
                                reservation.UserId,
                                book.Title
                            );
                            // After cancelling this reservation, try to move next student in queue
                            var queueSuccess = await ApproveNextAndHoldAsync(book._id.ToString());
                            Console.WriteLine($"🔄 [QUEUE PROCESSOR] Queue advancement for book '{bookTitle}': {(queueSuccess ? "Next student approved" : "No one in queue or insufficient copies")}");
                        }
                    }
                }

                Console.WriteLine($"📊 [QUEUE PROCESSOR] AutoCancelExpiredPickups complete: Cancelled {cancelledCount} reservations");
                return cancelledCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoCancelExpiredPickups] Error: {ex.Message}");
                return 0;
            }
        }

        public async Task<int> AutoRejectExpiredApprovalsAsync()
        {
            try
            {
                // Find all Approved reservations where 2 minutes have passed
                var now = DateTime.UtcNow;
                var expiredFilter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.Status, "Approved"),
                    Builders<Reservation>.Filter.Lt(r => r.ApprovalDate, now.AddMinutes(-2))
                );

                var expiredReservations = await _reservations.Find(expiredFilter).ToListAsync();
                Console.WriteLine($"🔄 [QUEUE PROCESSOR] AutoRejectExpiredApprovals: Found {expiredReservations.Count} expired Approved reservations");

                int rejectedCount = 0;

                foreach (var reservation in expiredReservations)
                {
                    var book = await FindBookByIdOrIsbn(reservation.BookId);
                    var bookTitle = book?.Title ?? "Unknown";
                    var studentNum = reservation.StudentNumber ?? "Unknown";
                    
                    Console.WriteLine($"🔄 [QUEUE PROCESSOR] Auto-rejecting reservation: Book='{bookTitle}', Student={studentNum}, ApprovalDate={reservation.ApprovalDate}");
                    
                    await ReleaseInventoryHoldAsync(reservation, book);

                    var update = Builders<Reservation>.Update
                        .Set(r => r.Status, "Rejected")
                        .Set(r => r.InventoryHoldActive, false)
                        .Set(r => r.Notes, "Auto-rejected: Approval expired (2-minute pickup window closed)");

                    var result = await _reservations.UpdateOneAsync(r => r._id == reservation._id, update);

                    if (result.ModifiedCount > 0)
                    {
                        rejectedCount++;
                        Console.WriteLine($"✅ [QUEUE PROCESSOR] Reservation auto-rejected: Book='{bookTitle}', Student={studentNum}");

                        // Send notification to student about expired reservation
                        if (book != null)
                        {
                            await _notificationService.CreateReservationExpiredNotificationAsync(
                                reservation.UserId,
                                book.Title
                            );
                            // After rejecting this reservation, try to move next student in queue
                            var queueSuccess = await ApproveNextAndHoldAsync(book._id.ToString());
                            Console.WriteLine($"🔄 [QUEUE PROCESSOR] Queue advancement for book '{bookTitle}': {(queueSuccess ? "Next student approved" : "No one in queue or insufficient copies")}");
                        }
                    }
                }

                Console.WriteLine($"📊 [QUEUE PROCESSOR] AutoRejectExpiredApprovals complete: Rejected {rejectedCount} reservations");
                return rejectedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoRejectExpiredApprovals] Error: {ex.Message}");
                return 0;
            }
        }

        // Process the next student in the reservation queue for a specific book.
        public async Task<bool> ProcessNextInQueueAsync(string bookId)
        {
            return await ApproveNextAndHoldAsync(bookId);
        }

        // Send pickup reminders (optional). Returns number of reminders sent.
        public async Task<int> SendPickupRemindersAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                // Find approved reservations which haven't had a pickup reminder sent yet
                var filter = Builders<Reservation>.Filter.And(
                    Builders<Reservation>.Filter.Eq(r => r.Status, "Approved"),
                    Builders<Reservation>.Filter.Exists(r => r.ApprovalDate, true),
                    Builders<Reservation>.Filter.Eq(r => r.PickupReminderSent, false)
                );

                var reservationsToRemind = await _reservations.Find(filter).ToListAsync();
                Console.WriteLine($"🔔 [QUEUE PROCESSOR] SendPickupReminders: Found {reservationsToRemind.Count} reservations to remind");
                
                int reminders = 0;

                foreach (var res in reservationsToRemind)
                {
                    if (!res.ApprovalDate.HasValue) continue;

                    var pickupDeadline = res.ApprovalDate.Value.AddMinutes(2);
                    var bookTitle = res.BookTitle ?? (await FindBookByIdOrIsbn(res.BookId))?.Title ?? "Unknown";
                    var studentNum = res.StudentNumber ?? "Unknown";

                    Console.WriteLine($"📨 [QUEUE PROCESSOR] Sending pickup reminder: Student={studentNum}, Book='{bookTitle}', Deadline={pickupDeadline:yyyy-MM-dd HH:mm}");
                    
                    // Optional behavior: send reminder once (we mark PickupReminderSent)
                    // We'll send the reminder immediately upon approval (this is "1 day before pickup deadline" when window is 24h)
                    await _notificationService.CreatePickupReminderNotificationAsync(
                        res.UserId,
                        bookTitle,
                        pickupDeadline
                    );

                    var update = Builders<Reservation>.Update.Set(r => r.PickupReminderSent, true);
                    await _reservations.UpdateOneAsync(r => r._id == res._id, update);
                    reminders++;
                    Console.WriteLine($"✅ [QUEUE PROCESSOR] Pickup reminder sent and marked: Student={studentNum}");
                }

                Console.WriteLine($"📊 [QUEUE PROCESSOR] SendPickupReminders complete: Sent {reminders} reminders");
                return reminders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendPickupRemindersAsync] Error: {ex.Message}");
                return 0;
            }
        }
    }
}