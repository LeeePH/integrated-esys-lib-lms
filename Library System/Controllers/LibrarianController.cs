using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SystemLibrary.Services;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SystemLibrary.Controllers
{
    [Authorize(Roles = "librarian")]
    public class LibrarianController : Controller
    {
        private readonly IReservationService _reservationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;
        private readonly IReturnService _returnService;
        private readonly IReportService _reportService;
        private readonly ITransactionService _transactionService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly IStudentProfileService _studentProfileService;
        private readonly IAuditLoggingHelper _auditLoggingHelper;
        private readonly IUnrestrictRequestService _unrestrictRequestService;
        // MOCK data service removed - now using enrollment system integration
        // private readonly IMOCKDataService _MOCKDataService;
        private readonly IBookImportService _bookImportService;
        private readonly IBookCopyService _bookCopyService;
        private readonly Cloudinary _cloudinary;

        public LibrarianController(
            IReservationService reservationService,
            IBookService bookService,
            IUserService userService,
            IReturnService returnService,
            IReportService reportService,
            ITransactionService transactionService,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IStudentProfileService studentProfileService,
            INotificationService notificationService,
            IAuditLoggingHelper auditLoggingHelper,
            IUnrestrictRequestService unrestrictRequestService,
            // IMOCKDataService MOCKDataService, // Removed - now using enrollment system integration
            IBookImportService bookImportService,
            IBookCopyService bookCopyService,
            Cloudinary cloudinary)
        {
            _reservationService = reservationService;
            _bookService = bookService;
            _userService = userService;
            _studentProfileService = studentProfileService;

            _returnService = returnService;
            _reportService = reportService;
            _transactionService = transactionService;
            _environment = environment;
            _configuration = configuration;
            _notificationService = notificationService;
            _auditLoggingHelper = auditLoggingHelper;
            _unrestrictRequestService = unrestrictRequestService;
            // _MOCKDataService = MOCKDataService; // Removed
            _bookImportService = bookImportService;
            _bookCopyService = bookCopyService;
            _cloudinary = cloudinary;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.Role = "Librarian";

            // Fetch all data in parallel for better performance
            var booksTask = _bookService.GetAllBooksAsync();
            var pendingReservationsTask = _reservationService.GetPendingReservationsAsync();
            var activeReservationsTask = _reservationService.GetActiveReservationsAsync();
            var studentProfilesTask = _studentProfileService.GetAllStudentProfilesAsync();
            var allReservationsTask = _reservationService.GetAllBorrowingsAsync();
            var usersTask = _userService.GetAllUsersAsync();

            await Task.WhenAll(booksTask, pendingReservationsTask, activeReservationsTask, studentProfilesTask, allReservationsTask, usersTask);

            var books = await booksTask;
            var pendingReservations = await pendingReservationsTask;
            var activeReservations = await activeReservationsTask;
            var studentProfiles = await studentProfilesTask;
            var allReservations = await allReservationsTask;
            var users = await usersTask;

            // Get return transactions for fee calculations and monthly returns
            var returnTransactions = await _returnService.GetAllReturnsAsync();

            // Calculate statistics
            var totalBooks = books.Count;
            var activeBorrowings = activeReservations.Count;
            // Count ALL returned books using ReturnTransaction collection (like reports tab)
            var totalReturns = returnTransactions.Count;
            var pendingReservationsCount = pendingReservations.Count;

            // Calculate pie chart data
            var overdueReservations = activeReservations
                .Where(r => r.DueDate.HasValue && DateTime.UtcNow > r.DueDate.Value)
                .ToList();

            // Distinct overdue accounts (unique users)
            var overdueAccounts = overdueReservations
                .Select(r => r.UserId)
                .Distinct()
                .Count();

            // Overdue books/items count
            var overdueBooks = overdueReservations.Count;

            // Total fees: sum of unpaid total penalties from return transactions
            // This covers late fees, damage, and lost penalties recorded at return time
            var totalFees = returnTransactions
                .Where(rt => !string.Equals(rt.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(rt => rt.TotalPenalty);

            // Calculate monthly borrowing vs returns data (show last 6 months for better visibility)
            var monthlyStats = new List<MonthlyStat>();
            
            // Show last 6 months for better data visibility
            var startDate = DateTime.UtcNow.AddMonths(-6);
            var endDate = DateTime.UtcNow;

            // Get borrowed reservations within the date range (like reports tab)
            var borrowedReservations = allReservations
                .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && r.ApprovalDate.Value <= endDate)
                .ToList();

            // Build monthly data within the range (like reports tab)
            var cursor = new DateTime(startDate.Year, startDate.Month, 1);
            var limit = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1);

            while (cursor < limit)
            {
                var monthStart = cursor;
                var monthEnd = cursor.AddMonths(1);

                var monthName = monthStart.ToString("MMM yyyy");

                // Count borrowings by ApprovalDate within [monthStart, monthEnd) - like reports tab
                var borrowings = borrowedReservations.Count(r =>
                    r.ApprovalDate.HasValue &&
                    r.ApprovalDate.Value >= monthStart &&
                    r.ApprovalDate.Value < monthEnd);

                // Count returns using ReturnTransaction.CreatedAt within [monthStart, monthEnd)
                var returns = returnTransactions.Count(rt =>
                    rt.CreatedAt >= monthStart &&
                    rt.CreatedAt < monthEnd);

                monthlyStats.Add(new MonthlyStat
                {
                    Month = monthName,
                    Borrowings = borrowings,
                    Returns = returns
                });

                cursor = cursor.AddMonths(1);
            }

            // Get recent reservations (last 5)
            var recentReservations = pendingReservations
                .OrderByDescending(r => r.ReservationDate)
                .Take(5)
                .ToList();

            // Calculate Most Borrowed Books (Top 5)
            var mostBorrowedBooks = allReservations
                .Where(r => r.ApprovalDate.HasValue)
                .GroupBy(r => r.BookId)
                .Select(g => new Models.MostBorrowedBook
                {
                    BookId = g.Key,
                    Title = books.FirstOrDefault(b => b._id.ToString() == g.Key)?.Title ?? "Unknown Book",
                    Author = books.FirstOrDefault(b => b._id.ToString() == g.Key)?.Author ?? "Unknown Author",
                    Category = books.FirstOrDefault(b => b._id.ToString() == g.Key)?.Subject ?? "Unknown",
                    BorrowCount = g.Count()
                })
                .OrderByDescending(b => b.BorrowCount)
                .Take(5)
                .ToList();

            // Calculate Most Active Students (Top 5)
            var mostActiveStudents = allReservations
                .Where(r => r.ApprovalDate.HasValue)
                .GroupBy(r => r.UserId)
                .Select(g =>
                {
                    StudentProfile? studentProfile = studentProfiles.FirstOrDefault(s => s.UserId.ToString() == g.Key);
                    User? user = users.FirstOrDefault(u => u._id.ToString() == g.Key);
                    
                    return new Models.MostActiveStudent
                    {
                        UserId = g.Key,
                        StudentNumber = studentProfile?.StudentNumber ?? "Unknown",
                        FullName = studentProfile?.FullName ?? user?.FullName ?? "Unknown Student",
                        Course = studentProfile?.Course ?? "Unknown",
                        BorrowCount = g.Count(),
                        ReturnCount = returnTransactions.Count(rt => rt.UserId.ToString() == g.Key),
                        TotalActivity = g.Count() + returnTransactions.Count(rt => rt.UserId.ToString() == g.Key)
                    };
                })
                .OrderByDescending(s => s.TotalActivity)
                .Take(5)
                .ToList();

            // Calculate Top Course/Programs (Top 5)
            var topCoursePrograms = studentProfiles
                .Where(s => !string.IsNullOrEmpty(s.Course))
                .GroupBy(s => s.Course)
                .Select(g => new Models.TopCourseProgram
                {
                    CourseProgram = g.Key,
                    StudentCount = g.Count(),
                    TotalBorrowings = allReservations
                        .Where(r => r.ApprovalDate.HasValue && 
                               g.Any(s => s.UserId.ToString() == r.UserId))
                        .Count(),
                    ActiveStudents = g.Count(s => users.Any(u => u._id.ToString() == s.UserId.ToString() && u.IsActive))
                })
                .OrderByDescending(c => c.TotalBorrowings)
                .Take(5)
                .ToList();

            var viewModel = new DashboardViewModel
            {
                Books = books,
                Reservations = pendingReservations,
                ActiveReturns = activeReservations,
                StudentProfiles = studentProfiles,
                Users = users,
                
                // Statistics
                TotalBooks = totalBooks,
                ActiveBorrowings = activeBorrowings,
                TotalReturns = totalReturns,
                PendingReservations = pendingReservationsCount,
                
                // Chart Data
                PieChartData = new ChartData
                {
                    OverDueAccounts = overdueAccounts,
                    OverdueBooks = overdueBooks,
                    TotalFees = (int)totalFees
                },
                MonthlyBorrowingVsReturns = new MonthlyData
                {
                    MonthlyStats = monthlyStats
                },
                
                // New Analytics Data
                MostBorrowedBooks = mostBorrowedBooks,
                MostActiveStudents = mostActiveStudents,
                TopCoursePrograms = topCoursePrograms,
                
                // Recent Activity
                RecentReservations = recentReservations
            };

            // Get notifications for the librarian
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var notifications = new List<Notification>();
            var unreadCount = 0;
            if (!string.IsNullOrEmpty(userId))
            {
                notifications = await _notificationService.GetUserNotificationsAsync(userId, 10);
                unreadCount = await _notificationService.GetUnreadCountAsync(userId);
            }

            // Pass notifications via ViewBag
            ViewBag.Notifications = notifications;
            ViewBag.UnreadNotificationCount = unreadCount;

            return View(viewModel);
        }


        // Catalog Management
        public async Task<IActionResult> Catalog()
        {
            ViewBag.Role = "Librarian";
            var books = await _bookService.GetAllBooksAsync();
            return View(books);
        }

        // MOCK data lookup methods removed - now using enrollment system integration
        // Books can be added manually through the catalog interface

        // Add selected MAC books with quantities
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMacBooks([FromBody] List<SelectedMacBook> selections)
        {
            if (selections == null || selections.Count == 0)
                return Json(new { success = false, message = "No selections provided" });

            int successCount = 0;
            foreach (var s in selections)
            {
                if (string.IsNullOrWhiteSpace(s.ISBN) || s.Quantity <= 0) continue;
                var ok = await _bookImportService.ProcessByIsbnAsync(s.ISBN, s.Quantity);
                if (ok) successCount++;
            }

            return Json(new { success = true, added = successCount });
        }

        public async Task<IActionResult> Transaction()
        {
            ViewBag.Role = "Librarian";
            return View();
        }

        // Returns Management
        public async Task<IActionResult> Return()
        {
            ViewBag.Role = "Librarian";
            var activeReservations = await _reservationService.GetActiveReservationsAsync();
            return View(activeReservations);
        }
        // Reservation Management Page
        public async Task<IActionResult> Reservation()


        {
            ViewBag.Role = "Librarian";
            ViewBag.Books = await _bookService.GetAllBooksAsync();
            ViewBag.Users = await _userService.GetAllUsersAsync();

            // Auto-cancel expired reservations before loading the page
            await _reservationService.AutoCancelExpiredPickupsAsync();

            var reservations = await _reservationService.GetPendingReservationsAsync();
            return View(reservations);
        }

        public async Task<IActionResult> Borrowing(int daysRange = 30)
        {
            ViewBag.Role = "Librarian";
            ViewBag.ActiveTab = "borrow";

            var reportData = await _reportService.GetBorrowingReportAsync(daysRange);

            return View(reportData);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetailedBorrowingData(string type, int daysRange = 30)
        {
            try
            {
                var detailedData = await _reportService.GetDetailedBorrowingDataAsync(type, daysRange);
                return Json(new { success = true, data = detailedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDetailedInventoryData(string type, int daysRange = 30)
        {
            try
            {
                var detailedData = await _reportService.GetDetailedInventoryDataAsync(type, daysRange);
                return Json(new { success = true, data = detailedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> Overdue(int daysRange = 30)
        {
            ViewBag.Role = "Librarian";
            ViewBag.ActiveTab = "overdue";

            var reportData = await _reportService.GetOverdueReportAsync(daysRange);

            return View(reportData);
        }

        public async Task<IActionResult> Inventory(int daysRange = 30)
        {
            ViewBag.Role = "Librarian";
            ViewBag.ActiveTab = "inventory";

            var reportData = await _reportService.GetInventoryReportAsync(daysRange);
            return View(reportData);
        }
        public async Task<IActionResult> Activity(int daysRange = 30)
        {
            ViewBag.Role = "Librarian";
            ViewBag.ActiveTab = "activity";

            var reportData = await _reportService.GetStudentActivityReportAsync(daysRange);

            return View(reportData);
        }

        // Process Return - GET
        [HttpGet]
        public async Task<IActionResult> ProcessReturn(string reservationId)
        {
            ViewBag.Role = "Librarian";

            if (string.IsNullOrEmpty(reservationId))
            {
                return NotFound();
            }

            var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
            if (reservation == null)
            {
                return NotFound();
            }

            var book = await _bookService.GetBookByIdAsync(reservation.BookId);
            if (book == null)
            {
                return NotFound();
            }

            // Calculate late fees in minutes
            var minutesLate = 0;
            if (reservation.DueDate.HasValue && DateTime.UtcNow > reservation.DueDate.Value)
            {
                minutesLate = (int)Math.Ceiling((DateTime.UtcNow - reservation.DueDate.Value).TotalMinutes);
            }

            var returnModel = new ReturnTransaction
            {
                ReservationId = ObjectId.Parse(reservation._id),
                BookId = ObjectId.Parse(reservation.BookId),
                UserId = ObjectId.Parse(reservation.UserId),
                BookTitle = book.Title,
                BorrowDate = reservation.ApprovalDate ?? reservation.ReservationDate,
                DueDate = reservation.DueDate ?? DateTime.UtcNow,
                ReturnDate = DateTime.UtcNow,
                DaysLate = minutesLate, // Store minutes in DaysLate field for compatibility
                LateFees = minutesLate * 10m, // ₱10 per minute
                BookCondition = "Good", // Default
                PenaltyAmount = 0m,
                TotalPenalty = minutesLate * 10m
            };

            return View(returnModel);
        }

        // Process Return - POST
        [HttpPost]
        public async Task<IActionResult> ProcessReturn(ReturnTransaction returnModel)
        {
            ViewBag.Role = "Librarian";

            if (!ModelState.IsValid)
            {
                return View(returnModel);
            }

            var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // ✅ Just set the ProcessedBy field - don't recalculate penalties!
            returnModel.ProcessedBy = librarianId != null ? ObjectId.Parse(librarianId) : null;

            var success = await _returnService.ProcessReturnAsync(returnModel);

            if (success)
            {
                TempData["SuccessMessage"] = $"Return processed successfully! Total Penalty: ₱{returnModel.TotalPenalty}";
                return RedirectToAction("Dashboard");
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to process return.";
                return View(returnModel);
            }
        }

        // View Return History
        public async Task<IActionResult> ReturnHistory()
        {
            ViewBag.Role = "Librarian";
            var returns = await _returnService.GetAllReturnsAsync();
            return View(returns);
        }

        // Search Return Transaction
        [HttpGet]
        public async Task<IActionResult> SearchReturn(string searchTerm)
        {
            ViewBag.Role = "Librarian";

            if (string.IsNullOrEmpty(searchTerm))
            {
                return RedirectToAction("Dashboard");
            }

            var result = await _returnService.SearchReturnAsync(searchTerm);

            if (result != null)
            {
                return View("ReturnDetails", result);
            }

            TempData["ErrorMessage"] = "Return transaction not found.";
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public IActionResult AddBook()
        {
            ViewBag.Role = "Librarian";
            return RedirectToAction("Catalog");
        }

        // Add New Book - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBook(Book book, IFormFile bookImage)
        {
            Console.WriteLine($"🔍 LIBRARIAN ADD BOOK: Method called with Title: {book.Title}");
            Console.WriteLine($"🔍 LIBRARIAN ADD BOOK: ModelState.IsValid: {ModelState.IsValid}");
            Console.WriteLine($"🔍 LIBRARIAN ADD BOOK: CopyManagementEnabled: {book.CopyManagementEnabled}");
            Console.WriteLine($"🔍 LIBRARIAN ADD BOOK: CopyPrefix: {book.CopyPrefix}");
            Console.WriteLine($"🔍 LIBRARIAN ADD BOOK: NextCopyNumber: {book.NextCopyNumber}");
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();
                Console.WriteLine($"❌ ModelState Errors: {string.Join(" | ", errors)}");
                TempData["ErrorMessage"] = "Invalid book data: " + string.Join(" | ", errors);
                return RedirectToAction("Catalog");
            }
            
            ViewBag.Role = "Librarian";

            try
            {
                // Handle image upload
                if (bookImage != null && bookImage.Length > 0)
                {
                    var imagePath = await SaveBookImageAsync(bookImage);
                    if (imagePath != null)
                    {
                        book.Image = imagePath;
                    }
                    else
                    {
                        // If image upload failed, use default
                        book.Image = "/images/default-book.png";
                    }
                }
                else
                {
                    // Use default image if no image uploaded
                    book.Image = "/images/default-book.png";
                }

                // Set initial values
                book.AvailableCopies = book.TotalCopies;
                book.CreatedAt = DateTime.UtcNow;

                var success = await _bookService.AddBookAsync(book);

                // If copy management is enabled, create individual copies
                if (success && book.CopyManagementEnabled)
                {
                    var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                    await _bookCopyService.CreateMultipleCopiesAsync(book._id.ToString(), book.TotalCopies, librarianId);
                }

                if (success)
                {
                    Console.WriteLine($"✅ LIBRARIAN ADD BOOK: Addition successful, calling audit logging...");
                    
                    // Log successful book addition
                    await _auditLoggingHelper.LogBookActionAsync(
                        "ADD_BOOK",
                        book._id.ToString(),
                        book.Title,
                        $"Book '{book.Title}' added by Librarian to library. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}, Available Copies: {book.AvailableCopies}",
                        true
                    );
                    
                    Console.WriteLine($"✅ LIBRARIAN ADD BOOK: Audit logging completed");
                    
                    TempData["SuccessMessage"] = "Book added successfully!";
                    return RedirectToAction("Dashboard");
                }
                else
                {
                    // Log failed book addition
                    await _auditLoggingHelper.LogBookActionAsync(
                        "ADD_BOOK",
                        book._id.ToString(),
                        book.Title,
                        $"Failed to add book '{book.Title}' - system error or duplicate ISBN",
                        false,
                        "Book addition failed"
                    );
                    
                    TempData["ErrorMessage"] = "Failed to add book.";
                }
            }
            catch (Exception ex)
            {
                // Log exception
                await _auditLoggingHelper.LogBookActionAsync(
                    "ADD_BOOK",
                    book._id.ToString(),
                    book.Title,
                    $"Exception occurred while adding book '{book.Title}': {ex.Message}",
                    false,
                    ex.Message
                );
                
                TempData["ErrorMessage"] = $"Error adding book: {ex.Message}";
            }

            return RedirectToAction("Catalog");
        }


        // Edit Book - GET
        [HttpGet]
        public async Task<IActionResult> EditBook(string id)
        {
            ViewBag.Role = "Librarian";

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var book = await _bookService.GetBookByIdAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            return RedirectToAction("Catalog");
        }


        // Edit Book - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(string id, Book book, IFormFile? bookImage)
        {
            Console.WriteLine($"🔍 LIBRARIAN EDIT BOOK: Method called with ID: {id}, Title: {book.Title}");
            
            if (string.IsNullOrEmpty(id))
                return BadRequest("Invalid book ID");

            try
            {
                // Get existing book first to preserve critical data
                var existingBook = await _bookService.GetBookByIdAsync(id);
                if (existingBook == null)
                    return NotFound();

                // Set the ID properly
                if (book._id == ObjectId.Empty)
                    book._id = existingBook._id;

                // Robustly handle IsActive (checkbox) in case model binder doesn't set it as expected
                try
                {
                    var isActiveVal = Request.Form["IsActive"].ToString();
                    if (!string.IsNullOrEmpty(isActiveVal))
                    {
                        // Handle multiple values (hidden false + checkbox true) or comma-separated values like "false,true"
                        if (isActiveVal.Contains(","))
                        {
                            var parts = isActiveVal.Split(',');
                            isActiveVal = parts[parts.Length - 1].Trim();
                        }

                        // Checkbox pattern: hidden false + checkbox true when checked
                        book.IsActive = isActiveVal.Equals("true", System.StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Librarian.EditBook] Error reading IsActive from form: {ex}");
                }

                // Robustly handle IsReferenceOnly (checkbox) in case model binder doesn't set it as expected
                try
                {
                    var isReferenceOnlyVal = Request.Form["IsReferenceOnly"].ToString();
                    if (!string.IsNullOrEmpty(isReferenceOnlyVal))
                    {
                        // Handle multiple values (hidden false + checkbox true) or comma-separated values like "false,true"
                        if (isReferenceOnlyVal.Contains(","))
                        {
                            var parts = isReferenceOnlyVal.Split(',');
                            isReferenceOnlyVal = parts[parts.Length - 1].Trim();
                        }

                        // Checkbox pattern: hidden false + checkbox true when checked
                        book.IsReferenceOnly = isReferenceOnlyVal.Equals("true", System.StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Librarian.EditBook] Error reading IsReferenceOnly from form: {ex}");
                }

                // Validate the form data
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(kvp => kvp.Value.Errors.Count > 0)
                        .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}")
                        .ToList();

                    TempData["ErrorMessage"] = "Invalid book data: " + string.Join(" | ", errors);
                    Console.WriteLine("❌ ModelState Errors: " + string.Join(" | ", errors));

                    // Also log posted IsActive and existingBook value for debugging
                    Console.WriteLine($"[Librarian.EditBook] Posted IsActive (form)='{Request.Form["IsActive"]}', Bound book.IsActive={book.IsActive}, existingBook.IsActive={existingBook.IsActive}");

                    return RedirectToAction("Catalog");
                }

                // Handle image upload
                if (bookImage != null && bookImage.Length > 0)
                {
                    if (!existingBook.Image.Contains("default-book.png"))
                        DeleteBookImage(existingBook.Image);

                    var imagePath = await SaveBookImageAsync(bookImage);
                    book.Image = imagePath ?? existingBook.Image;
                }
                else
                {
                    // Keep existing image
                    book.Image = existingBook.Image;
                }

                // Ensure AvailableCopies is not set from form - let UpdateBookAsync calculate it
                book.AvailableCopies = 0; // This will be recalculated in UpdateBookAsync
                book.CreatedAt = existingBook.CreatedAt; // Preserve creation date
                Console.WriteLine($"[Librarian.EditBook] existingBook.IsActive={existingBook.IsActive}, incoming book.IsActive={book.IsActive}");
                // Update record
                var success = await _bookService.UpdateBookAsync(id, book);
                Console.WriteLine($"[Librarian.EditBook] UpdateBookAsync returned: {success}");

                // Handle copy management for updates
                if (success && book.CopyManagementEnabled)
                {
                    // Check if this is the first time enabling copy management
                    if (!existingBook.CopyManagementEnabled)
                    {
                        // Create individual copies for the first time
                        var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                        await _bookCopyService.CreateMultipleCopiesAsync(id, book.TotalCopies, librarianId);
                    }
                    else if (book.TotalCopies > existingBook.TotalCopies)
                    {
                        // Add more copies if quantity increased
                        var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                        var additionalCopies = book.TotalCopies - existingBook.TotalCopies;
                        await _bookCopyService.CreateMultipleCopiesAsync(id, additionalCopies, librarianId);
                    }
                }

                if (success)
                {
                    Console.WriteLine($"✅ LIBRARIAN EDIT BOOK: Update successful, calling audit logging...");
                    
                    // Log successful book update
                    await _auditLoggingHelper.LogBookActionAsync(
                        "EDIT_BOOK",
                        id,
                        book.Title,
                        $"Book '{book.Title}' updated. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}, Available Copies: {book.AvailableCopies}",
                        true
                    );
                    
                    Console.WriteLine($"✅ LIBRARIAN EDIT BOOK: Audit logging completed");
                    
                    TempData["BookUpdated"] = true;
                    return RedirectToAction("Catalog");
                }
                else
                {
                    // Log failed book update
                    await _auditLoggingHelper.LogBookActionAsync(
                        "EDIT_BOOK",
                        id,
                        book.Title,
                        $"Failed to update book '{book.Title}' - system error",
                        false,
                        "Book update failed"
                    );
                    
                    TempData["ErrorMessage"] = "Failed to update book.";
                }
            }
            catch (Exception ex)
            {
                // Log exception
                await _auditLoggingHelper.LogBookActionAsync(
                    "EDIT_BOOK",
                    id,
                    book.Title,
                    $"Exception occurred while updating book '{book.Title}': {ex.Message}",
                    false,
                    ex.Message
                );
                
                TempData["ErrorMessage"] = $"Error updating book: {ex.Message}";
                Console.WriteLine("❌ Exception: " + ex);
            }

            return RedirectToAction("Catalog");
        }


        // Delete Book - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBook(string id)
        {
            try
            {
                // Get book to delete its image
                var book = await _bookService.GetBookByIdAsync(id);

                if (book == null)
                {
                    TempData["ErrorMessage"] = "Book not found.";
                    return RedirectToAction("Dashboard");
                }

                // Check if book has borrowed copies
                if (book.BorrowedCopies > 0)
                {
                    TempData["ErrorMessage"] = "Failed to delete book. Make sure no copies are currently borrowed.";
                    return RedirectToAction("Dashboard");
                }

                var success = await _bookService.DeleteBookAsync(id);

                if (success)
                {
                    // Log successful book deletion
                    await _auditLoggingHelper.LogBookActionAsync(
                        "DELETE_BOOK",
                        id,
                        book.Title,
                        $"Book '{book.Title}' deleted from library. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}",
                        true
                    );
                    
                    // Delete image file if it's not the default
                    if (!book.Image.Contains("default-book.png"))
                    {
                        DeleteBookImage(book.Image);
                    }

                    TempData["SuccessMessage"] = "Book deleted successfully!";
                }
                else
                {
                    // Log failed book deletion
                    await _auditLoggingHelper.LogBookActionAsync(
                        "DELETE_BOOK",
                        id,
                        book.Title,
                        $"Failed to delete book '{book.Title}' - system error or copies still borrowed",
                        false,
                        "Book deletion failed"
                    );
                    
                    TempData["ErrorMessage"] = "Failed to delete book. Make sure no copies are currently borrowed.";
                }
            }
            catch (Exception ex)
            {
                // Log exception
                await _auditLoggingHelper.LogBookActionAsync(
                    "DELETE_BOOK",
                    id,
                    "Unknown Book",
                    $"Exception occurred while deleting book: {ex.Message}",
                    false,
                    ex.Message
                );
                
                TempData["ErrorMessage"] = $"Error deleting book: {ex.Message}";
            }

            return RedirectToAction("Dashboard");
        }

        // Helper method to save book image (Cloudinary)
        private async Task<string> SaveBookImageAsync(IFormFile imageFile)
        {
            try
            {
                // Validate file
                var maxFileSize = _configuration.GetValue<long>("FileUpload:MaxFileSize", 5242880); // 5MB default
                if (imageFile.Length > maxFileSize)
                {
                    TempData["ErrorMessage"] = "Image file is too large. Maximum size is 5MB.";
                    return null;
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["ErrorMessage"] = "Invalid file type. Only JPG, PNG, and GIF are allowed.";
                    return null;
                }

                if (_cloudinary == null)
                {
                    TempData["ErrorMessage"] = "Image service is not configured.";
                    return null;
                }

                await using var stream = imageFile.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(imageFile.FileName, stream),
                    Folder = "library-books"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Store the Cloudinary URL in the database
                    return uploadResult.SecureUrl?.ToString() ?? uploadResult.Url?.ToString();
                }

                TempData["ErrorMessage"] = "Failed to upload image to Cloudinary.";
                return null;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error uploading image: {ex.Message}";
                return null;
            }
        }

        // Helper method to delete book image
        private void DeleteBookImage(string imagePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(imagePath) && !imagePath.Contains("default-book.png"))
                {
                    var fullPath = Path.Combine(_environment.WebRootPath, imagePath.TrimStart('/'));
                    if (System.IO.File.Exists(fullPath))
                    {
                        System.IO.File.Delete(fullPath);
                    }
                }
            }
            catch (Exception)
            {
                // Log error but don't stop the operation
            }
        }

        // Reservation Management
        [HttpPost]
        public async Task<IActionResult> ApproveReservation(string reservationId)
        {
            try
            {
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                
                var success = await _reservationService.ApproveReservationAsync(reservationId, librarianId);

                if (success)
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "APPROVE_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Reservation approved for pickup. Due date: {reservation?.DueDate?.ToString("MMM dd, yyyy") ?? "Not set"}",
                        true
                    );
                    
                    return Json(new { success = true, message = "Reservation approved successfully." });
                }
                else
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "APPROVE_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        "Failed to approve reservation - system error or invalid state",
                        false,
                        "Reservation approval failed"
                    );
                    
                    return Json(new { success = false, message = "Failed to approve reservation." });
                }
            }
            catch (Exception ex)
            {
                await _auditLoggingHelper.LogBorrowingActionAsync(
                    "APPROVE_RESERVATION",
                    reservationId,
                    "Unknown Book",
                    "Unknown Student",
                    $"Exception occurred during reservation approval: {ex.Message}",
                    false,
                    ex.Message
                );
                
                return Json(new { success = false, message = "An error occurred while approving the reservation." });
            }
        }

        public async Task<IActionResult> Reports(string timeRange = "ThisMonth", DateTime? from = null, DateTime? to = null)
        {
            ViewBag.Role = "Librarian";

            var viewModel = await _reportService.GetCompleteReportAsync(timeRange, from, to);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RejectReservation(string reservationId)
        {
            try
            {
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                
                var success = await _reservationService.RejectReservationAsync(reservationId, librarianId);

                if (success)
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "REJECT_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Reservation rejected by librarian. Reason: Manual rejection",
                        true
                    );
                    
                    return Json(new { success = true, message = "Reservation rejected successfully." });
                }
                else
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "REJECT_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        "Failed to reject reservation - system error or invalid state",
                        false,
                        "Reservation rejection failed"
                    );
                    
                    return Json(new { success = false, message = "Failed to reject reservation." });
                }
            }
            catch (Exception ex)
            {
                await _auditLoggingHelper.LogBorrowingActionAsync(
                    "REJECT_RESERVATION",
                    reservationId,
                    "Unknown Book",
                    "Unknown Student",
                    $"Exception occurred during reservation rejection: {ex.Message}",
                    false,
                    ex.Message
                );
                
                return Json(new { success = false, message = "An error occurred while rejecting the reservation." });
            }
        }

        public async Task<IActionResult> SearchStudent(string studentId)
        {
            if (string.IsNullOrEmpty(studentId))
            {
                return Json(new { success = false, message = "Student ID is required" });
            }

            var student = await _transactionService.GetStudentInfoAsync(studentId);

            if (student == null)
            {
                return Json(new { success = false, message = "Student not found" });
            }

            return Json(new { success = true, data = student });
        }
        public async Task<IActionResult> SearchBook(string bookId)
        {
            if (string.IsNullOrEmpty(bookId))
            {
                return Json(new { success = false, message = "Book ID is required" });
            }

            var book = await _transactionService.GetBookInfoAsync(bookId);

            if (book == null)
            {
                return Json(new { success = false, message = "Book not found" });
            }

            return Json(new { success = true, data = book });
        }

        

        // API: Check Eligibility
        [HttpPost]
        public async Task<IActionResult> CheckEligibility([FromBody] CheckEligibilityRequest request)
        {
            if (string.IsNullOrEmpty(request.StudentId) || string.IsNullOrEmpty(request.BookId))
            {
                return Json(new { success = false, message = "Student ID and Book ID are required" });
            }

            var eligibility = await _transactionService.CheckBorrowingEligibilityAsync(
                request.StudentId,
                request.BookId
            );

            return Json(new
            {
                success = true,
                data = eligibility
            });
        }

        // API: Process Borrowing
        [HttpPost]
        public async Task<IActionResult> ProcessBorrowing([FromBody] DirectBorrowingRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId) || string.IsNullOrEmpty(request.BookId))
                {
                    return Json(new { success = false, message = "Student ID and Book ID are required" });
                }

                // Get librarian ID from claims
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // If not found, try alternative claim types
                if (string.IsNullOrEmpty(librarianId))
                {
                    librarianId = User.FindFirst("UserId")?.Value;
                }

                if (string.IsNullOrEmpty(librarianId))
                {
                    librarianId = User.FindFirst("sub")?.Value;
                }

                // Set request properties
                request.LibrarianId = librarianId;
                request.BorrowDate = DateTime.UtcNow;
                request.LoanPeriodDays = 14; // 14 days loan period
                request.DueDate = request.BorrowDate.AddDays(request.LoanPeriodDays);

                // Process the borrowing
                var success = await _transactionService.ProcessDirectBorrowingAsync(request);

                if (success)
                {
                    return Json(new { success = true, message = "Book borrowed successfully" });
                }

                return Json(new { success = false, message = "Failed to process borrowing. Please check eligibility." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessBorrowing: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        [HttpGet]
        public async Task<IActionResult> SearchActiveReservation(string bookId)
        {
            try
            {
                Console.WriteLine($"[SearchActiveReservation] Searching for bookId/ISBN: {bookId}");

                if (string.IsNullOrEmpty(bookId))
                {
                    return Json(new { success = false, message = "Book ID is required" });
                }

                // First, try to find the book by ID or ISBN
                Book book = null;

                if (ObjectId.TryParse(bookId, out ObjectId objectId))
                {
                    book = await _bookService.GetBookByIdAsync(bookId);
                }

                if (book == null)
                {
                    var allBooks = await _bookService.GetAllBooksAsync();
                    book = allBooks.FirstOrDefault(b =>
                        b.ISBN?.Equals(bookId, StringComparison.OrdinalIgnoreCase) == true ||
                        b._id.ToString() == bookId);
                }

                if (book == null)
                {
                    return Json(new { success = false, message = "Book not found" });
                }

                // Search for BORROWED reservation only
                var allReservations = await _reservationService.GetAllReservationsInRangeAsync(
                    DateTime.UtcNow.AddYears(-10),
                    DateTime.UtcNow.AddYears(10));

                var reservation = allReservations.FirstOrDefault(r =>
                    r.BookId == book._id.ToString() && r.Status == "Borrowed");

                if (reservation == null)
                {
                    return Json(new { success = false, message = $"No borrowed books found for '{book.Title}'" });
                }

                // Get book and user details
                var user = await _userService.GetUserByIdAsync(reservation.UserId);

                if (user == null)
                {
                    return Json(new { success = false, message = "User information not found" });
                }

                // Calculate minutes late
                var minutesLate = 0;
                if (reservation.DueDate.HasValue && DateTime.UtcNow > reservation.DueDate.Value)
                {
                    minutesLate = (int)Math.Ceiling((DateTime.UtcNow - reservation.DueDate.Value).TotalMinutes);
                }

                // Calculate late fee: ₱10 per minute overdue
                var lateFee = minutesLate > 0 ? minutesLate * 10m : 0m;

                var result = new
                {
                    reservationId = reservation._id,
                    bookId = reservation.BookId,
                    userId = reservation.UserId,
                    bookTitle = book.Title,
                    isbn = book.ISBN,
                    borrowerName = $"{user.FullName}",
                    borrowDate = reservation.ApprovalDate ?? reservation.ReservationDate,
                    dueDate = reservation.DueDate ?? DateTime.UtcNow,
                    daysBorrowed = (DateTime.UtcNow - (reservation.ApprovalDate ?? reservation.ReservationDate)).Days,
                    daysLate = minutesLate, // Store minutes in daysLate for compatibility
                    minutesLate = minutesLate,
                    lateFee = lateFee,
                    isOverdue = minutesLate > 0
                };

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        // API: Process return transaction
        [HttpPost]
        public async Task<IActionResult> ProcessReturnTransaction([FromBody] ProcessReturnRequest request)
        {
            try
            {
                Console.WriteLine($"[ProcessReturnTransaction] Starting return process...");
                Console.WriteLine($"[ProcessReturnTransaction] BookCondition: {request.BookCondition}");
                Console.WriteLine($"[ProcessReturnTransaction] DamageType: {request.DamageType}");
                Console.WriteLine($"[ProcessReturnTransaction] DamagePenalty: {request.DamagePenalty}");
                Console.WriteLine($"[ProcessReturnTransaction] TotalPenalty: {request.TotalPenalty}");

                if (string.IsNullOrEmpty(request.ReservationId))
                {
                    return Json(new { success = false, message = "Reservation ID is required" });
                }

                // Validate ObjectIds
                if (!ObjectId.TryParse(request.ReservationId, out ObjectId reservationObjectId))
                {
                    return Json(new { success = false, message = "Invalid Reservation ID format" });
                }

                if (!ObjectId.TryParse(request.BookId, out ObjectId bookObjectId))
                {
                    return Json(new { success = false, message = "Invalid Book ID format" });
                }

                if (!ObjectId.TryParse(request.UserId, out ObjectId userObjectId))
                {
                    return Json(new { success = false, message = "Invalid User ID format" });
                }

                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                // Parse dates
                DateTime borrowDate = DateTime.Parse(request.BorrowDate);
                DateTime dueDate = DateTime.Parse(request.DueDate);

                // Create return transaction
                var returnTransaction = new ReturnTransaction
                {
                    ReservationId = reservationObjectId,
                    BookId = bookObjectId,
                    UserId = userObjectId,
                    BookTitle = request.BookTitle,
                    BorrowDate = borrowDate,
                    DueDate = dueDate,
                    ReturnDate = DateTime.UtcNow,
                    DaysLate = request.DaysLate,
                    LateFees = request.LateFees,
                    BookCondition = request.BookCondition,
                    DamageType = request.DamageType,  // ⭐ This should already be here
                    DamagePenalty = request.DamagePenalty,  // ⭐ This should already be here
                    PenaltyAmount = request.PenaltyAmount,
                    TotalPenalty = request.TotalPenalty,
                    PaymentStatus = "Pending",
                    ProcessedBy = !string.IsNullOrEmpty(librarianId) ? ObjectId.Parse(librarianId) : null
                };

                var success = await _returnService.ProcessReturnAsync(returnTransaction);

                if (success)
                {
                    // Note: Penalty records are already created by ReturnService.ProcessReturnAsync()
                    // No need to call AddPenaltyToStudentAsync() as it would create duplicate records

                    // If book is marked as Lost, automatically restrict the user account
                    bool accountRestricted = false;
                    if (request.BookCondition == "Lost")
                    {
                        var restrictReason = $"Account restricted due to lost book: {request.BookTitle}";
                        accountRestricted = await _userService.RestrictUserAsync(request.UserId, restrictReason);
                    }

                    // Send notification
                    await _notificationService.CreateBookReturnedNotificationAsync(
                        request.UserId,
                        request.BookTitle,
                        returnTransaction.TotalPenalty,
                        request.BookCondition,
                        request.DaysLate
                    );

                    if (request.BookCondition == "Lost")
                    {
                        if (accountRestricted)
                        {
                            return Json(new { 
                                success = true, 
                                message = "Book marked as Lost. Student account has been automatically restricted.",
                                accountRestricted = true
                            });
                        }
                        else
                        {
                            return Json(new { 
                                success = true, 
                                message = "Book marked as Lost. Warning: Failed to restrict student account.",
                                accountRestricted = false
                            });
                        }
                    }

                    return Json(new { success = true, message = "Return processed successfully" });
                }

                return Json(new { success = false, message = "Failed to process return" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessReturnTransaction] EXCEPTION: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // API: Restrict user account
        [HttpPost]
        public async Task<IActionResult> RestrictAccount([FromBody] RestrictAccountRequest request)
        {
            try
            {
                Console.WriteLine($"[RestrictAccount] Attempting to restrict user: {request.UserId}");

                if (string.IsNullOrEmpty(request.UserId))
                {
                    return Json(new { success = false, message = "User ID is required" });
                }

                var success = await _userService.RestrictUserAsync(request.UserId, request.Reason);
                Console.WriteLine($"[RestrictAccount] Result: {success}");

                if (success)
                {
                    return Json(new { success = true, message = "Account restricted successfully" });
                }

                return Json(new { success = false, message = "Failed to restrict account" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RestrictAccount] Error: {ex.Message}");
                Console.WriteLine($"[RestrictAccount] StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetBorrowedHistory()
        {
            try
            {
                var reservations = await _reservationService.GetAllBorrowingHistoryAsync();

                // Fetch books and users in parallel to improve performance
                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];
                    bool isOverdue = reservation.Status == "Borrowed" && reservation.DueDate.HasValue
                        && DateTime.UtcNow > reservation.DueDate.Value;

                    // Handle null coalescing in C# instead of LINQ
                    var displayDate = reservation.ApprovalDate ?? reservation.ReservationDate;

                    return new
                    {
                        reservationId = reservation._id,
                        userId = reservation.UserId,
                        bookId = reservation.BookId,
                        userName = user?.FullName ?? "Unknown User",
                        userIdShort = reservation.UserId?.Length >= 8 ? reservation.UserId.Substring(0, 8) + "..." : reservation.UserId,
                        studentNumber = reservation.StudentNumber ?? "N/A",
                        bookTitle = book?.Title ?? "Unknown Book",
                        isbn = book?.ISBN ?? "N/A",
                        reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                        approvalDate = reservation.ApprovalDate?.ToString("MM/dd/yyyy") ?? "N/A",
                        dueDate = reservation.DueDate?.ToString("MM/dd/yyyy") ?? "N/A",
                        returnDate = reservation.ReturnDate?.ToString("MM/dd/yyyy") ?? "Not Returned",
                        status = reservation.Status,
                        isOverdue = isOverdue
                    };
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                // Log ex.StackTrace somewhere to get details
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get pending reservations
        [HttpGet]
        public async Task<IActionResult> GetPendingReservations()
        {
            try
            {
                var reservations = await _reservationService.GetPendingReservationsAsync();
                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];
                    return new
                    {
                        reservationId = reservation._id,
                        userId = reservation.UserId,
                        bookId = reservation.BookId,
                        userName = user?.FullName ?? "Unknown User",
                        userIdShort = reservation.UserId?.Length >= 8 ? reservation.UserId.Substring(0, 8) + "..." : reservation.UserId,
                        studentNumber = reservation.StudentNumber ?? "N/A",
                        bookTitle = book?.Title ?? "Unknown Book",
                        isbn = book?.ISBN ?? "N/A",
                        reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                        status = reservation.Status,
                        borrowType = reservation.BorrowType ?? "ONLINE",
                        approvalDate = reservation.ReservationDate.ToString("O")
                    };
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get approved reservations with auto-reject and timer
        [HttpGet]
        public async Task<IActionResult> GetApprovedReservations()
        {
            try
            {
                // Auto-cancel expired approvals first (2-minute pickup window)
                await _reservationService.AutoCancelExpiredPickupsAsync();

                // Get all reservations and filter for "Approved" status
                var allReservations = await _reservationService.GetAllReservationsInRangeAsync(
                    DateTime.UtcNow.AddYears(-10),
                    DateTime.UtcNow.AddYears(10));
                var reservations = allReservations.Where(r => r.Status == "Approved").ToList();

                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];

                    return new
                    {
                        reservationId = reservation._id,
                        userId = reservation.UserId,
                        bookId = reservation.BookId,
                        userName = user?.FullName ?? "Unknown User",
                        userIdShort = reservation.UserId?.Length >= 8 ? reservation.UserId.Substring(0, 8) + "..." : reservation.UserId,
                        studentNumber = reservation.StudentNumber ?? "N/A",
                        bookTitle = book?.Title ?? "Unknown Book",
                        isbn = book?.ISBN ?? "N/A",
                        reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                        approvalDate = reservation.ApprovalDate?.ToString("O") ?? reservation.ReservationDate.ToString("O"),
                        borrowType = reservation.BorrowType ?? "ONLINE",
                        status = reservation.Status,
                        minutesRemaining = reservation.MinutesRemainingForPickup,
                        isExpired = reservation.IsApprovalExpired
                    };
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get borrowed reservations (actually checked out books)
        [HttpGet]
        public async Task<IActionResult> GetBorrowedReservations()
        {
            try
            {
                // Get all reservations and filter for ALL borrowed-related statuses
                var allReservations = await _reservationService.GetAllReservationsInRangeAsync(
                    DateTime.UtcNow.AddYears(-10),
                    DateTime.UtcNow.AddYears(10));

                // Include: Borrowed, Returned, Overdue, Damaged, Lost
                var reservations = allReservations.Where(r =>
                    r.Status == "Borrowed" ||
                    r.Status == "Returned" ||
                    r.Status == "Overdue" ||
                    r.Status == "Damaged" ||
                    r.Status == "Lost").ToList();

                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];

                    return new
                    {
                        reservationId = reservation._id,
                        userId = reservation.UserId,
                        bookId = reservation.BookId,
                        userName = user?.FullName ?? "Unknown User",
                        userIdShort = reservation.UserId?.Length >= 8 ? reservation.UserId.Substring(0, 8) + "..." : reservation.UserId,
                        studentNumber = reservation.StudentNumber ?? "N/A",
                        bookTitle = book?.Title ?? "Unknown Book",
                        isbn = book?.ISBN ?? "N/A",
                        reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                        approvalDate = reservation.ApprovalDate?.ToString("MM/dd/yyyy") ?? "N/A",
                        dueDate = reservation.DueDate?.ToString("MM/dd/yyyy") ?? "N/A",
                        borrowType = reservation.BorrowType ?? "ONLINE",
                        status = reservation.Status,
                        daysRemaining = reservation.DaysRemaining,
                        isOverdue = reservation.IsOverdue
                    };
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Get cancelled reservations
        [HttpGet]
        public async Task<IActionResult> GetCancelledReservations()
        {
            try
            {
                var allReservations = await _reservationService.GetAllReservationsInRangeAsync(
                    DateTime.UtcNow.AddYears(-10),
                    DateTime.UtcNow.AddYears(10));
                var reservations = allReservations.Where(r => r.Status == "Rejected").ToList();

                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];
                    return new
                    {
                        reservationId = reservation._id,
                        userId = reservation.UserId,
                        bookId = reservation.BookId,
                        userName = user?.FullName ?? "Unknown User",
                        userIdShort = reservation.UserId?.Length >= 8 ? reservation.UserId.Substring(0, 8) + "..." : reservation.UserId,
                        studentNumber = reservation.StudentNumber ?? "N/A",
                        bookTitle = book?.Title ?? "Unknown Book",
                        isbn = book?.ISBN ?? "N/A",
                        reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                        borrowType = reservation.BorrowType ?? "ONLINE",
                        status = reservation.Status
                    };
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Mark as borrowed - NEW WORKFLOW: Only when student actually picks up the book
        [HttpPost]
        public async Task<IActionResult> MarkAsBorrowed(string reservationId)
        {
            try
            {
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                
                var success = await _reservationService.MarkAsBorrowedAsync(reservationId, librarianId);

                if (success)
                {
                    // Log successful borrowing
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "MARK_AS_BORROWED",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Book marked as borrowed. Due date: {reservation?.DueDate?.ToString("MMM dd, yyyy") ?? "Not set"}. Copy ID: {reservation?.CopyIdentifier ?? "Not assigned"}",
                        true
                    );
                    
                    return Json(new { success = true, message = "Book marked as borrowed successfully!" });
                }
                else
                {
                    // Log failed borrowing
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "MARK_AS_BORROWED",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        "Failed to mark book as borrowed - reservation may not be in 'Approved' status or no copies available",
                        false,
                        "Book borrowing failed"
                    );
                    
                    return Json(new { success = false, message = "Failed to mark book as borrowed. Reservation may not be in 'Approved' status." });
                }
            }
            catch (Exception ex)
            {
                // Log exception
                await _auditLoggingHelper.LogBorrowingActionAsync(
                    "MARK_AS_BORROWED",
                    reservationId,
                    "Unknown Book",
                    "Unknown Student",
                    $"Exception occurred during book borrowing: {ex.Message}",
                    false,
                    ex.Message
                );
                
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Mark as returned
        [HttpPost]
        public async Task<IActionResult> MarkAsReturned(string reservationId, string remarks)
        {
            try
            {
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                if (reservation == null)
                    return Json(new { success = false, message = "Reservation not found" });

                var returnTransaction = new ReturnTransaction
                {
                    ReservationId = ObjectId.Parse(reservationId),
                    UserId = ObjectId.Parse(reservation.UserId),
                    BookId = ObjectId.Parse(reservation.BookId),
                    BookTitle = reservation.BookTitle,
                    BorrowDate = reservation.ReservationDate,
                    DueDate = reservation.DueDate ?? DateTime.UtcNow,
                    ReturnDate = DateTime.UtcNow,
                    BookCondition = "Good",
                    Remarks = remarks,
                    PaymentStatus = "Pending"
                };

                await _returnService.ProcessReturnAsync(returnTransaction);

                return Json(new { success = true, message = "Book marked as returned" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Mark with action (Overdue, Lost, Damaged)
        [HttpPost]  
        public async Task<IActionResult> MarkWithAction(string reservationId, string actionType, string remarks)
        {
            try
            {
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                if (reservation == null)
                    return Json(new { success = false, message = "Reservation not found" });

                var condition = actionType == "Damaged" ? "Damage" :
                               actionType == "Lost" ? "Lost" : "Good";

                var returnTransaction = new ReturnTransaction
                {
                    ReservationId = ObjectId.Parse(reservationId),
                    UserId = ObjectId.Parse(reservation.UserId),
                    BookId = ObjectId.Parse(reservation.BookId),
                    BookTitle = reservation.BookTitle,
                    BorrowDate = reservation.ReservationDate,
                    DueDate = reservation.DueDate ?? DateTime.UtcNow,
                    ReturnDate = DateTime.UtcNow,
                    BookCondition = condition,
                    Remarks = remarks,
                    PaymentStatus = "Pending"
                };

                await _returnService.ProcessReturnAsync(returnTransaction);

                // If book is marked as Lost, automatically restrict the user account
                if (actionType == "Lost")
                {
                    var restrictReason = $"Account restricted due to lost book: {reservation.BookTitle}";
                    var restrictSuccess = await _userService.RestrictUserAsync(reservation.UserId, restrictReason);
                    
                    if (restrictSuccess)
                    {
                        return Json(new { 
                            success = true, 
                            message = $"Book marked as Lost. Student account has been automatically restricted.",
                            accountRestricted = true
                        });
                    }
                    else
                    {
                        return Json(new { 
                            success = true, 
                            message = $"Book marked as Lost. Warning: Failed to restrict student account.",
                            accountRestricted = false
                        });
                    }
                }

                return Json(new { success = true, message = $"Book marked as {actionType}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Auto-cancel expired reservations
        [HttpPost]
        public async Task<IActionResult> AutoCancelReservation(string reservationId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
                var success = await _reservationService.CancelReservationAsync(
                    reservationId,
                    userId,
                    "Cancelled by librarian");

                if (success)
                {
                    return Json(new { success = true, message = "Reservation auto-cancelled" });
                }

                return Json(new { success = false, message = "Unable to cancel reservation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Request to unrestrict user account
        [HttpPost]
        public async Task<IActionResult> UnrestrictUser(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User ID is required" });
                }

                // Get user information
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                // Get student profile for student number
                var studentProfile = await _userService.GetStudentProfileAsync(userId);
                var studentNumber = studentProfile?.StudentNumber ?? "N/A";

                // Get librarian information
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var librarianName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

                // Create unrestrict request
                var request = new UnrestrictRequest
                {
                    UserId = userId,
                    StudentName = user.FullName,
                    StudentNumber = studentNumber,
                    RequestedBy = librarianId,
                    RequestedByName = librarianName,
                    RequestedByRole = "Librarian",
                    Reason = "Librarian requested account unrestriction",
                    Status = "Pending"
                };

                var success = await _unrestrictRequestService.CreateRequestAsync(request);
                
                if (success)
                {
                    // Create notification for admin
                    var adminNotification = new Notification
                    {
                        UserId = "admin", // This will be handled by admin notification system
                        Type = "UNRESTRICT_REQUEST",
                        Title = "New Unrestrict Request",
                        Message = $"Librarian {librarianName} has requested to unrestrict student {user.FullName}",
                        BookTitle = "",
                        ReservationId = ""
                    };

                    await _notificationService.CreateNotificationAsync(adminNotification);

                    return Json(new { success = true, message = "Request sent to admin. Please wait for admin approval." });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to create unrestrict request" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Get renewal requests
        [HttpGet]
        public async Task<IActionResult> GetRenewalRequests()
        {
            try
            {
                var reservations = await _reservationService.GetRenewalRequestsAsync();
                var books = await Task.WhenAll(reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)));
                var users = await Task.WhenAll(reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)));

                var enrichedData = reservations.Select((reservation, index) => new
                {
                    reservationId = reservation._id,
                    userId = reservation.UserId,
                    bookId = reservation.BookId,
                    userName = users[index]?.FullName ?? "Unknown User",
                    studentNumber = reservation.StudentNumber ?? "N/A",
                    bookTitle = books[index]?.Title ?? "Unknown Book",
                    isbn = books[index]?.ISBN ?? "N/A",
                    reservationDate = reservation.ReservationDate.ToString("MM/dd/yyyy"),
                    borrowType = reservation.BorrowType ?? "ONLINE",
                    status = reservation.Status,
                    dueDate = reservation.DueDate?.ToString("MM/dd/yyyy") ?? "N/A"
                }).ToList();

                return Json(new { success = true, data = enrichedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Approve renewal request
        [HttpPost]
        public async Task<IActionResult> ApproveRenewal(string reservationId)
        {
            try
            {
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _reservationService.ApproveRenewalAsync(reservationId, librarianId);

                if (success)
                {
                    var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                    var book = await _bookService.GetBookByIdAsync(reservation.BookId);
                    var user = await _userService.GetUserByIdAsync(reservation.UserId);

                    // Create notification for student
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = reservation.UserId,
                        Type = "RENEWAL_APPROVED",
                        Title = "RENEWAL APPROVED",
                        Message = $"Your renewal request for '{book?.Title}' has been approved. New due date: {reservation.DueDate?.ToString("MMM dd, yyyy")}",
                        BookTitle = book?.Title ?? "Unknown Book",
                        ReservationId = reservation._id
                    });

                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "RENEWAL_APPROVED",
                        reservationId,
                        book?.Title ?? "Unknown Book",
                        reservation.StudentNumber ?? "N/A",
                        $"Renewal approved for {user?.FullName ?? "Unknown"}. New due date: {reservation.DueDate?.ToString("MMM dd, yyyy")}",
                        true
                    );

                    return Json(new { success = true, message = "Renewal approved successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to approve renewal" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Reject renewal request
        [HttpPost]
        public async Task<IActionResult> RejectRenewal(string reservationId)
        {
            try
            {
                var librarianId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _reservationService.RejectRenewalAsync(reservationId, librarianId);

                if (success)
                {
                    var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                    var book = await _bookService.GetBookByIdAsync(reservation.BookId);

                    // Create notification for student
                    await _notificationService.CreateNotificationAsync(new Notification
                    {
                        UserId = reservation.UserId,
                        Type = "RENEWAL_REJECTED",
                        Title = "RENEWAL REJECTED",
                        Message = $"Your renewal request for '{book?.Title}' has been rejected. Please return the book by the original due date.",
                        BookTitle = book?.Title ?? "Unknown Book",
                        ReservationId = reservation._id
                    });

                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "RENEWAL_REJECTED",
                        reservationId,
                        book?.Title ?? "Unknown Book",
                        reservation.StudentNumber ?? "N/A",
                        $"Renewal rejected for {reservation.StudentNumber}",
                        true
                    );

                    return Json(new { success = true, message = "Renewal rejected" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to reject renewal" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<List<CourseSummaryViewModel>> GetCourseBorrowingSummaryAsync(string timeRange)
        {
            var allBorrowings = await _reservationService.GetAllBorrowingsAsync();


            var allStudents = await _userService.GetAllStudentsAsync();


            DateTime startDate = timeRange switch
            {
                "Last7Days" => DateTime.UtcNow.AddDays(-7),
                "Last30Days" => DateTime.UtcNow.AddDays(-30),
                "Last90Days" => DateTime.UtcNow.AddDays(-90),
                _ => DateTime.MinValue
            };

            var filteredBorrowings = allBorrowings
                .Where(b => (b.ApprovalDate ?? b.ReservationDate) >= startDate)
                .ToList();

            var allCourses = allStudents.Select(s => s.Course).Distinct();

            var courseSummary = allCourses.Select(course =>
            {
                var borrowingsForCourse = filteredBorrowings
                    .Join(allStudents,
                          r => r.UserId.ToString(),
                          s => s._id.ToString(),
                          (r, s) => new { r, s })
                    .Where(x => x.s.Course == course);

                return new CourseSummaryViewModel
                {
                    Course = course,
                    TotalBorrowings = borrowingsForCourse.Count(),
                    TotalReturned = borrowingsForCourse.Count(x => x.r.Status == "Returned" || x.r.Status == "Damaged" || x.r.Status == "Lost"),
                    TotalOverdue = borrowingsForCourse.Count(x =>
                        x.r.Status == "Borrowed" &&
                        x.r.DueDate.HasValue &&
                        x.r.DueDate.Value < DateTime.UtcNow)
                };
            })
            .OrderByDescending(c => c.TotalBorrowings)
            .ToList();

            return courseSummary;
        }


        // Get book copies for modal display
        [HttpGet]
        public async Task<IActionResult> GetBookCopies(string bookId)
        {
            try
            {
                if (string.IsNullOrEmpty(bookId))
                {
                    return Json(new { success = false, message = "Book ID is required" });
                }

                var copies = await _bookCopyService.GetBookCopiesByBookIdAsync(bookId);
                var book = await _bookService.GetBookByIdAsync(bookId);

                if (book == null)
                {
                    return Json(new { success = false, message = "Book not found" });
                }

                var copyData = copies.Select(copy => new
                {
                    id = copy._id,
                    copyId = copy.CopyId,
                    barcode = copy.Barcode,
                    status = copy.StatusDisplayName,
                    condition = copy.ConditionDisplayName,
                    location = copy.Location,
                    acquisitionDate = copy.AcquisitionDate.ToString("MMM dd, yyyy"),
                    lastBorrowedDate = copy.LastBorrowedDate?.ToString("MMM dd, yyyy") ?? "Never",
                    borrowCount = copy.BorrowCount,
                    notes = copy.Notes ?? "",
                    isAvailable = copy.IsAvailable,
                    isBorrowed = copy.IsBorrowed,
                    isLost = copy.IsLost,
                    isDamaged = copy.IsDamaged,
                    needsRepair = copy.NeedsRepair
                }).ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        bookTitle = book.Title,
                        bookAuthor = book.Author,
                        bookIsbn = book.ISBN,
                        totalCopies = copies.Count,
                        copies = copyData
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Update copy status
        [HttpPost]
        public async Task<IActionResult> UpdateCopyStatus([FromBody] UpdateCopyStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.CopyId))
                {
                    return Json(new { success = false, message = "Copy ID is required" });
                }

                bool success = false;
                switch (request.Action.ToLower())
                {
                    case "status":
                        success = await _bookCopyService.UpdateCopyStatusAsync(request.CopyId, request.Status);
                        break;
                    case "condition":
                        success = await _bookCopyService.UpdateCopyConditionAsync(request.CopyId, request.Condition, request.Notes);
                        break;
                    case "lost":
                        success = await _bookCopyService.MarkCopyAsLostAsync(request.CopyId, request.Notes);
                        break;
                    case "damaged":
                        success = await _bookCopyService.MarkCopyAsDamagedAsync(request.CopyId, request.Notes);
                        break;
                    case "found":
                        success = await _bookCopyService.MarkCopyAsFoundAsync(request.CopyId);
                        break;
                    case "repaired":
                        success = await _bookCopyService.MarkCopyAsRepairedAsync(request.CopyId);
                        break;
                    default:
                        return Json(new { success = false, message = "Invalid action" });
                }

                if (success)
                {
                    return Json(new { success = true, message = "Copy updated successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to update copy" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Request models (add to the end of LibrarianController.cs file)
        public class ProcessReturnRequest
        {
            public string ReservationId { get; set; }
            public string BookId { get; set; }
            public string UserId { get; set; }
            public string BookTitle { get; set; }
            public string BorrowDate { get; set; }
            public string DueDate { get; set; }
            public string ReturnDate { get; set; }
            public int DaysLate { get; set; }
            public decimal LateFees { get; set; }
            public string BookCondition { get; set; }
            public string DamageType { get; set; }  // ⭐ ADD THIS
            public decimal DamagePenalty { get; set; }  // ⭐ ADD THIS
            public decimal PenaltyAmount { get; set; }
            public decimal TotalPenalty { get; set; }
            public string PaymentStatus { get; set; }
        }

        public class RestrictAccountRequest
        {
            public string UserId { get; set; }
            public string Reason { get; set; }
        }

        // Notification methods
        public async Task<IActionResult> Notifications()
        {
            ViewBag.Role = "Librarian";
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
    }

    public class CheckEligibilityRequest
    {
        public string StudentId { get; set; }
        public string BookId { get; set; }
    }

    public class UpdateCopyStatusRequest
    {
        public string CopyId { get; set; }
        public string Action { get; set; } // status, condition, lost, damaged, found, repaired
        public BookCopyStatus Status { get; set; }
        public CopyCondition Condition { get; set; }
        public string Notes { get; set; }
    }
}

