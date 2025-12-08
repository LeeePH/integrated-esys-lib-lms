using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SystemLibrary.Services;
using SystemLibrary.ViewModels;
using SystemLibrary.Models;

namespace SystemLibrary.Controllers
{
    [Authorize(Roles = "student")]
    public class StudentController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IReservationService _reservationService;
        private readonly IUserService _userService;
        private readonly INotificationService _notificationService;
        private readonly IPenaltyService _penaltyService;

        public StudentController(
            IBookService bookService,
            IReservationService reservationService,
            IUserService userService,
            INotificationService notificationService,
            IPenaltyService penaltyService)
        {
            _bookService = bookService;
            _reservationService = reservationService;
            _userService = userService;
            _notificationService = notificationService;
            _penaltyService = penaltyService;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.Role = "Student";
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var fullName = User.FindFirst("FullName")?.Value;
            string displayName = "Student";

            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = await _userService.GetUserByIdAsync(userId);

                // Get student account to fetch names from enrollment system
                Console.WriteLine($"[Dashboard] Fetching student account for userId: {userId}");
                var studentAccount = await _userService.GetStudentAccountAsync(userId);

                // Priority: Enrollment name > Local user name > Claim name > Username > Default
                if (studentAccount != null && !string.IsNullOrWhiteSpace(studentAccount.FullName))
                {
                    displayName = studentAccount.FullName;
                    Console.WriteLine($"[Dashboard] Using enrollment/student account name: {displayName}");
                }
                else if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
                {
                    displayName = user.FullName;
                    Console.WriteLine($"[Dashboard] Using local user name: {displayName}");
                }
                else if (!string.IsNullOrWhiteSpace(fullName))
                {
                    displayName = fullName;
                    Console.WriteLine($"[Dashboard] Using claim name: {displayName}");
                }
                else if (user != null && !string.IsNullOrWhiteSpace(user.Username))
                {
                    displayName = user.Username;
                    Console.WriteLine($"[Dashboard] Using username: {displayName}");
                }
                else
                {
                    Console.WriteLine($"[Dashboard] Using default name: Student");
                }

                // Get only "Borrowed" books for the dashboard (books that student has actually picked up)
                // "Approved" books should not appear until they are marked as "Borrowed"
                var activeReservations = await _reservationService.GetUserBorrowedBooksAsync(userId);

                // Get real notifications from database
                var notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);
                var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

                // Process overdue penalties in real-time before fetching
                await _penaltyService.ProcessUserOverduePenaltiesAsync(userId);

                // Get penalty information
                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                var totalPendingPenalties = pendingPenalties.Sum(p => p.Amount);

                // Update user penalty status
                await _penaltyService.UpdateUserPenaltyStatusAsync(userId);

                var currentBorrowings = new List<CurrentBorrowingViewModel>();
                foreach (var reservation in activeReservations)
                {
                    var book = await _bookService.GetBookByIdAsync(reservation.BookId);
                    if (book != null)
                    {
                        var minutesRemaining = reservation.DueDate.HasValue
                            ? (int)(reservation.DueDate.Value - DateTime.UtcNow).TotalMinutes
                            : 0;

                        currentBorrowings.Add(new CurrentBorrowingViewModel
                        {
                            BookTitle = book.Title ?? "Unknown Book",
                            BookAuthor = book.Author ?? "Unknown Author",
                            BookCover = !string.IsNullOrEmpty(book.Image)
                                ? book.Image
                                : "/images/default-book.png",
                            BorrowedDate = reservation.ApprovalDate ?? reservation.ReservationDate,
                            DueDate = reservation.DueDate ?? DateTime.UtcNow.AddMinutes(1),
                            IsOverdue = minutesRemaining < 0,
                            DaysRemaining = minutesRemaining // Actually stores minutes for testing
                        });
                    }
                }

                var overdueCount = currentBorrowings.Count(b => b.IsOverdue);

                var viewModel = new StudentDashboardViewModel
                {
                    StudentName = displayName,
                    ActiveBorrowings = currentBorrowings.Count,
                    OverdueBooks = overdueCount,
                    CurrentBorrowings = currentBorrowings,
                    Notifications = notifications,
                    UnreadNotificationCount = unreadCount,
                    HasPendingPenalties = pendingPenalties.Any(),
                    TotalPendingPenalties = totalPendingPenalties,
                    PendingPenalties = pendingPenalties
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard] ERROR: {ex.Message}");
                Console.WriteLine($"[Dashboard] StackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "Error loading dashboard: " + ex.Message;
                return View(new StudentDashboardViewModel
                {
                    StudentName = displayName,
                    ActiveBorrowings = 0,
                    OverdueBooks = 0,
                    CurrentBorrowings = new List<CurrentBorrowingViewModel>(),
                    Notifications = new List<Notification>(),
                    UnreadNotificationCount = 0
                });
            }
        }


        public async Task<IActionResult> BrowseBooks()
        {
            ViewBag.Role = "Student";
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            // Get dynamic content
            var bookOfTheDay = await _bookService.GetBookOfTheDayAsync();
            var trendingBooks = await _bookService.GetTrendingBooksAsync(10);
            var recommendedBooks = await _bookService.GetRecommendedBooksAsync(count: 10);

            // Pass data to view using ViewBag
            ViewBag.BookOfTheDay = bookOfTheDay;
            ViewBag.TrendingBooks = trendingBooks;
            ViewBag.RecommendedBooks = recommendedBooks;
            ViewBag.AllBooks = await _bookService.GetAllBooksAsync();

            return View(recommendedBooks); // Pass recommended as default model
        }

        public async Task<IActionResult> Borrowings()
        {
            ViewBag.Role = "Student";
            Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Add("Pragma", "no-cache");
            Response.Headers.Add("Expires", "0");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var viewModel = await _reservationService.GetUserBorrowingsAsync(userId);
            return View(viewModel);
        }

        public async Task<IActionResult> Notifications()
        {
            ViewBag.Role = "Student";
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var notifications = await _notificationService.GetAllUserNotificationsAsync(userId, 100);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            var viewModel = new StudentDashboardViewModel
            {
                StudentName = user.FullName ?? user.Username,
                Notifications = notifications,
                UnreadNotificationCount = unreadCount
            };

            return View(viewModel);
        }

        // Mark notification as read
        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(string notificationId)
        {
            try
            {
                var success = await _notificationService.MarkAsReadAsync(notificationId);
                return Json(new { success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Mark all notifications as read
        [HttpPost]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _notificationService.MarkAllAsReadAsync(userId);
                return Json(new { success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Archive notification
        [HttpPost]
        public async Task<IActionResult> ArchiveNotification(string notificationId, bool isArchived)
        {
            try
            {
                var success = await _notificationService.ArchiveNotificationAsync(notificationId, isArchived);
                return Json(new { success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Delete notification
        [HttpPost]
        public async Task<IActionResult> DeleteNotification(string notificationId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Verify the notification belongs to the user before deleting
                var notification = await _notificationService.GetNotificationByIdAsync(notificationId);

                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found." });
                }

                if (notification.UserId != userId)
                {
                    return Json(new { success = false, message = "Unauthorized access." });
                }

                var success = await _notificationService.DeleteNotificationAsync(notificationId);
                return Json(new { success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Clear all read notifications
        [HttpPost]
        public async Task<IActionResult> ClearAllReadNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not found." });
                }

                var success = await _notificationService.ClearAllReadNotificationsAsync(userId);
                return Json(new { success });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetArchivedNotifications()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not found" });
                }

                var archivedNotifications = await _notificationService.GetArchivedNotificationsAsync(userId)
                                           ?? new List<Notification>();

                return Json(new
                {
                    success = true,
                    notifications = archivedNotifications.Select(n => new
                    {
                        _id = n._id.ToString(),
                        title = n.Title,
                        message = n.Message,
                        createdAt = n.CreatedAt,
                        isRead = n.IsRead,
                        icon = n.Icon
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAccount(UpdateAccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var accountData = await _userService.GetStudentAccountAsync(userId);
                TempData["ErrorMessage"] = "Please check your input and try again.";
                return View("Account", accountData);
            }

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var success = await _userService.UpdatePasswordAsync(currentUserId, model.NewPassword);

            if (success)
            {
                TempData["SuccessMessage"] = "Password updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update password. Please try again.";
            }

            return RedirectToAction("Account");
        }

        [HttpGet]
        public async Task<IActionResult> Account()
        {
            ViewBag.Role = "Student";
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Account");

            var accountData = await _userService.GetStudentAccountAsync(userId);
            return View(accountData);
        }

        [HttpGet]
        public async Task<IActionResult> CheckPenalties()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { hasPenalties = false, penaltyCount = 0, totalAmount = 0 });
                }

                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();

                return Json(new
                {
                    hasPenalties = pendingPenalties.Any(),
                    penaltyCount = pendingPenalties.Count,
                    totalAmount = pendingPenalties.Sum(p => p.Amount)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking penalties: {ex.Message}");
                return Json(new { hasPenalties = false, penaltyCount = 0, totalAmount = 0 });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ReserveBook(string bookId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Check if user is restricted
            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "User not found. Please log in again." });
            }

            if (user.IsRestricted)
            {
                return Json(new
                {
                    success = false,
                    message = "Your account has been restricted. You cannot borrow books at this time. Please contact the library administrator."
                });
            }

            // Check for pending penalties
            var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
            var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();

            if (pendingPenalties.Any())
            {
                var totalPenalties = pendingPenalties.Sum(p => p.Amount);
                return Json(new
                {
                    success = false,
                    message = $"You have {pendingPenalties.Count} pending penalty(ies) totaling ₱{totalPenalties:F2}. Please settle your penalties before borrowing books. Visit your Account page to view penalty details."
                });
            }

            var result = await _reservationService.CreateReservationAsync(userId, bookId);
            
            if (result.Success)
            {
                // Get book details for notification
                var book = await _bookService.GetBookByIdAsync(bookId);

                // Get the newly created reservation
                var reservations = await _reservationService.GetUserReservationsAsync(userId);
                var reservation = reservations.OrderByDescending(r => r.ReservationDate)
                    .FirstOrDefault(r => r.BookId == bookId);

                if (book != null && reservation != null)
                {
                    // Create notification
                    await _notificationService.CreateReservationCreatedNotificationAsync(
                        userId,
                        book.Title,
                        reservation._id
                    );
                }

                return Json(new { success = true, message = "Book reservation submitted successfully!" });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    errorCode = result.ErrorCode,
                    message = result.Message ?? "Failed to submit book reservation. Book may not be available."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromWaitlist(string reservationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var success = await _reservationService.RemoveFromWaitlistAsync(reservationId, userId);

            if (success)
            {
                return Json(new { success = true, message = "Book removed from waitlist successfully!" });
            }
            else
            {
                return Json(new { success = false, message = "Failed to remove book from waitlist." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RequestRenewal(string reservationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var success = await _reservationService.RequestRenewalAsync(reservationId);

            if (success)
            {
                // Get reservation and book details for notification
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                var book = await _bookService.GetBookByIdAsync(reservation.BookId);

                if (book != null)
                {
                    await _notificationService.CreateRenewalRequestedNotificationAsync(
                        userId,
                        book.Title
                    );
                }

                TempData["SuccessMessage"] = "Renewal request submitted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to submit renewal request.";
            }
            return RedirectToAction("Borrowings");
        }

        [HttpPost]
        public async Task<IActionResult> ReportBookIssue(string reservationId, string issueDescription)
        {
            TempData["SuccessMessage"] = "Issue reported successfully. A librarian will contact you soon.";
            return RedirectToAction("Borrowings");
        }
    }
}