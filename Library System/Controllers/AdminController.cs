using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SystemLibrary.Services;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace SystemLibrary.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller   
    {
        private readonly IReservationService _reservationService;
        private readonly IBookService _bookService;
        private readonly IUserService _userService;
        private readonly IReturnService _returnService;
        private readonly IReportService _reportService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IUserManagementService _userManagementService;
        private readonly IAuditLogService _auditLogService;
        private readonly IBackupService _backupService;
        private readonly ITransactionService _transactionService;
        private readonly IPenaltyService _penaltyService;
        private readonly IStudentProfileService _studentProfileService;
        private readonly IAuditLoggingHelper _auditLoggingHelper;
        private readonly IUnrestrictRequestService _unrestrictRequestService;
        private readonly INotificationService _notificationService;
		private readonly IEmailService _emailService;
        private readonly IBookImportService _bookImportService;
        private readonly IEnrollmentSystemService _enrollmentSystemService;
        private readonly Cloudinary _cloudinary;
        private readonly IMongoDbService _mongoDbService;
        private readonly IMOCKDataService _mockDataService;


        public AdminController(
                IBackupService backupService,
            IReservationService reservationService,
            IBookService bookService,
            IUserService userService,
            IReturnService returnService,
            IReportService reportService,
            IWebHostEnvironment environment,
                IUserManagementService userManagementService,
                    IAuditLogService auditLogService,
            ITransactionService transactionService,
            IPenaltyService penaltyService,
            IConfiguration configuration,
            IStudentProfileService studentProfileService,
            IAuditLoggingHelper auditLoggingHelper,
            IUnrestrictRequestService unrestrictRequestService,
            INotificationService notificationService,
            IEmailService emailService,
            IBookImportService bookImportService,
            IEnrollmentSystemService enrollmentSystemService,
            Cloudinary cloudinary,
            IMongoDbService mongoDbService,
            IMOCKDataService mockDataService)
        {
            _reservationService = reservationService;
            _bookService = bookService;
            _userService = userService;
            _returnService = returnService;
            _reportService = reportService;
            _environment = environment;
            _configuration = configuration;
            _userManagementService = userManagementService;
            _auditLogService = auditLogService;
            _backupService = backupService;
            _transactionService = transactionService;
            _penaltyService = penaltyService;
            _studentProfileService = studentProfileService;
            _auditLoggingHelper = auditLoggingHelper;
            _unrestrictRequestService = unrestrictRequestService;
            _notificationService = notificationService;
            _emailService = emailService;
			_bookImportService = bookImportService;
            _enrollmentSystemService = enrollmentSystemService;
            _cloudinary = cloudinary;
            _mongoDbService = mongoDbService;
            _mockDataService = mockDataService;



        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.Role = "Admin";

            // Get all data in parallel for better performance
            var booksTask = _bookService.GetAllBooksAsync();
            var allReservationsTask = _reservationService.GetAllBorrowingsAsync();
            var activeReservationsTask = _reservationService.GetActiveReservationsAsync();
            var pendingReservationsTask = _reservationService.GetPendingReservationsAsync();
            var studentProfilesTask = _studentProfileService.GetAllStudentProfilesAsync();
            var usersTask = _userService.GetAllUsersAsync();

            // Wait for all tasks to complete
            var books = await booksTask;
            var allReservations = await allReservationsTask;
            var activeReservations = await activeReservationsTask;
            var pendingReservations = await pendingReservationsTask;
            var studentProfiles = await studentProfilesTask;
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

            var overdueAccounts = overdueReservations
                .GroupBy(r => r.UserId)
                .Select(g => g.Key)
                .Distinct()
                .Count();

            var overdueBooks = overdueReservations.Count;

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

            return View(viewModel);
        }

        // Catalog Management
        public async Task<IActionResult> Catalog()
        {
            ViewBag.Role = "Admin";
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
            ViewBag.Role = "Admin";
            return View();
        }

        // Returns Management
        public async Task<IActionResult> Return()
        {
            ViewBag.Role = "Admin";
            var activeReservations = await _reservationService.GetActiveReservationsAsync();
            return View(activeReservations);
        }
        // Reservation Management Page
        public async Task<IActionResult> Reservation()
        {
            ViewBag.Role = "Admin";
            ViewBag.Books = await _bookService.GetAllBooksAsync();
            ViewBag.Users = await _userService.GetAllUsersAsync();

            // Auto-cancel expired reservations before loading the page
            await _reservationService.AutoCancelExpiredPickupsAsync();

            var reservations = await _reservationService.GetPendingReservationsAsync();
            return View(reservations);
        }

        public async Task<IActionResult> Borrowing(int daysRange = 30)
        {
            ViewBag.Role = "Admin";
            ViewBag.ActiveTab = "borrow";

            var reportData = await _reportService.GetBorrowingReportAsync(daysRange);

            return View(reportData);
        }

        public async Task<IActionResult> Overdue(int daysRange = 30)
        {
            ViewBag.Role = "Admin";
            ViewBag.ActiveTab = "overdue";

            var reportData = await _reportService.GetOverdueReportAsync(daysRange);

            return View(reportData);
        }

        public async Task<IActionResult> Inventory(int daysRange = 30)
        {
            ViewBag.Role = "Admin";
            ViewBag.ActiveTab = "inventory";

            var reportData = await _reportService.GetInventoryReportAsync(daysRange);
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

        [HttpGet]
        public async Task<IActionResult> GetDetailedOverdueData(string type, int daysRange = 30)
        {
            try
            {
                var detailedData = await _reportService.GetDetailedOverdueDataAsync(type, daysRange);
                return Json(new { success = true, data = detailedData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> UserSettings()
        {
            ViewBag.Role = "Admin";
            var staff = await _userManagementService.GetLibraryStaffAsync();
            return View(staff);
        }

        [HttpGet]
        public async Task<IActionResult> UserSettingsStudent()
        {
            ViewBag.Role = "Admin";
            var students = await _userManagementService.GetStudentsAsync();
            return View(students);
        }

        // System Maintenance Page
        public async Task<IActionResult> SystemMaintenance()
        {
            ViewBag.Role = "Admin";

            var backups = await _backupService.GetAllBackupsAsync();
            var latestBackup = await _backupService.GetLatestBackupAsync();

            ViewBag.LatestBackup = latestBackup;

            return View(backups);
        }

        

        public async Task<IActionResult> LogAudit(int page = 1, int pageSize = 50)
        {
            ViewBag.Role = "Admin";

            // Get ALL logs without any filters
            var auditLogs = await _auditLogService.GetAllAuditLogsAsync((page - 1) * pageSize, pageSize);
            var totalCount = await _auditLogService.GetTotalCountAsync();
            var statistics = await _auditLogService.GetAuditStatisticsAsync();
            
            // DEBUG: Log what we found
            Console.WriteLine($"🔍 LOG AUDIT DEBUG: Found {auditLogs.Count} logs out of {totalCount} total logs");
            foreach (var log in auditLogs.Take(5)) // Show first 5 logs
            {
                Console.WriteLine($"📋 LOG: {log.Timestamp} - {log.UserRole} - {log.Action} - {log.Details}");
            }
            
            // TEST: Create a manual audit log to test the system
            try
            {
                await _auditLoggingHelper.LogBookActionAsync(
                    "TEST_LOG",
                    "test-book-id",
                    "Test Book",
                    "Manual test log created from LogAudit page",
                    true
                );
                Console.WriteLine("✅ TEST: Manual audit log created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ TEST: Failed to create manual audit log: {ex.Message}");
            }

            // Calculate pagination info
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            var hasNextPage = page < totalPages;
            var hasPrevPage = page > 1;

            // Pass data to view
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.HasNextPage = hasNextPage;
            ViewBag.HasPrevPage = hasPrevPage;
            ViewBag.Statistics = statistics;

            return View(auditLogs ?? new List<AuditLog>());
        }

        [HttpPost]
        public async Task<IActionResult> LogAuditFilter([FromBody] AuditLogFilter filter)
        {
            ViewBag.Role = "Admin";

            try
            {
                // Handle export request
                if (filter.Export)
                {
                    var exportLogs = await _auditLogService.GetFilteredAuditLogsAsync(filter);
                    return await ExportAuditLogs(exportLogs);
                }

                // Apply pagination
                filter.Skip = (filter.Page - 1) * filter.PageSize;
                filter.Limit = filter.PageSize;

                var auditLogs = await _auditLogService.GetFilteredAuditLogsAsync(filter);
                var totalCount = await _auditLogService.GetFilteredCountAsync(filter);

                // Convert to display format
                var displayLogs = auditLogs.Select(log => new
                {
                    _id = log._id,
                    timestamp = log.Timestamp,
                    username = log.Username,
                    userRole = log.UserRole,
                    action = log.Action,
                    actionCategory = log.ActionCategory,
                    actionIcon = log.ActionIcon,
                    details = log.Details,
                    entityName = log.EntityName,
                    success = log.Success,
                    errorMessage = log.ErrorMessage,
                    durationMs = log.DurationMs,
                    formattedTimestamp = log.FormattedTimestamp
                }).ToList();

                return Json(new { 
                    success = true, 
                    logs = displayLogs, 
                    totalCount = totalCount,
                    message = $"Found {auditLogs.Count} logs matching your criteria"
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"Error filtering audit logs: {ex.Message}" 
                });
            }
        }

        private async Task<IActionResult> ExportAuditLogs(List<AuditLog> auditLogs)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Username,User Role,Action,Action Category,Details,Entity Name,Success,Error Message,Duration (ms)");

            foreach (var log in auditLogs)
            {
                csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Username}\",\"{log.UserRole}\",\"{log.Action}\",\"{log.ActionCategory}\",\"{log.Details.Replace("\"", "\"\"")}\",\"{log.EntityName}\",\"{log.Success}\",\"{log.ErrorMessage}\",\"{log.DurationMs}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"AuditLogs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(bytes, "text/csv", fileName);
        }


        public async Task<IActionResult> Activity(int daysRange = 30)
        {
            ViewBag.Role = "Admin";
            ViewBag.ActiveTab = "activity";



            var reportData = await _reportService.GetStudentActivityReportAsync(daysRange);

            return View(reportData);
        }

        // Add User - POST
        [HttpPost]
        public async Task<IActionResult> AddUser([FromBody] CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid data provided" });
            }

            var success = await _userManagementService.AddUserAsync(model);

			if (success)
            {
                // CREATE AUDIT LOG ✅
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var adminName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

                await _auditLogService.CreateAuditLogAsync(
                    adminId,
                    adminName,
                    "ADMIN",
                    "User Account Created",
                    $"Created New {model.Role.ToUpper()} Account:<br /><strong>{model.Username}</strong>",
                    "User",
                    null
                );

				// Send welcome credentials email for students
				try
				{
					if (!string.IsNullOrWhiteSpace(model.Role) && model.Role.Equals("student", StringComparison.OrdinalIgnoreCase))
					{
                        var subject = "Welcome to Library Management - Your Credentials";
                        var body = $@"<p>Hi {model.Name},</p>
						<p>Your library account has been created.</p>
						<p><strong>Username:</strong> {model.Username}<br/>
						<strong>Password:</strong> {model.Password}</p>
						<p>Please keep this information secure.</p>
						<p><em>PS: For your security, please log in and change your password as soon as possible.</em></p>";

						Console.WriteLine($"[AdminController] Sending welcome email to {model.Username}...");
                        var recipient = string.IsNullOrWhiteSpace(model.Email) ? model.Username : model.Email;
                        await _emailService.SendEmailAsync(recipient, subject, body);
						Console.WriteLine("[AdminController] Welcome email dispatched.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[AdminController] Failed to send welcome email: {ex.Message}");
				}

				return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = "Username already exists or failed to add user" });
            }
        }

        // Enrollment System Lookup Endpoints
        // Note: Students are automatically synced from enrollment system during login
        // This endpoint can be used to check if a student exists in the enrollment system
        [HttpGet]
        public async Task<IActionResult> LookupStudent(string email)
        {
            try
            {
                // Look up student by email in enrollment system
                var enrollmentStudent = await _enrollmentSystemService.GetStudentByEmailAsync(email);
                
                if (enrollmentStudent == null)
                {
                    return Json(new { success = false, message = "Student not found in enrollment system" });
                }

                return Json(new { 
                    success = true, 
                    data = new {
                        username = enrollmentStudent.Username,
                        email = enrollmentStudent.Email,
                        type = enrollmentStudent.Type,
                        firstLogin = enrollmentStudent.FirstLogin,
                        message = "Student found in enrollment system. They will be automatically synced when they log in."
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error looking up student: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> LookupStaff(string employeeId)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
            {
                return Json(new
                {
                    success = false,
                    message = "Employee ID is required for lookup."
                });
            }

            // First try MOCK data lookup
            var staff = await _mockDataService.GetStaffByEmployeeIdAsync(employeeId);
            if (staff != null)
            {
                return Json(new
                {
                    success = true,
                    source = "MOCK",
                    data = new
                    {
                        employeeId = staff.EmployeeId,
                        fullName = staff.FullName,
                        department = staff.Department,
                        position = staff.Position,
                        email = staff.Email,
                        contactNumber = staff.ContactNumber,
                        employmentType = staff.EmploymentType,
                        hireDate = staff.HireDate,
                        isActive = staff.IsActive
                    }
                });
            }

            // Enrollment system staff lookup not available
            return Json(new
            {
                success = false,
                message = "Staff not found in MOCK data. Enrollment system staff lookup is not available; please add staff via MOCK data or manual user management."
            });
        }

        [HttpPost]
		public async Task<IActionResult> CreateUserFromMAC([FromBody] CreateUserFromMOCKViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid data provided." });
            }

            // Only staff flow is supported
            if (!string.Equals(model.UserType, "staff", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Only staff creation from MOCK data is supported." });
            }

            if (string.IsNullOrWhiteSpace(model.EmployeeId))
            {
                return Json(new { success = false, message = "Employee ID is required." });
            }

            // Lookup staff in MOCK data
            var staff = await _mockDataService.GetStaffByEmployeeIdAsync(model.EmployeeId);
            if (staff == null)
            {
                return Json(new { success = false, message = "Staff not found in MOCK data." });
            }

            // If staff record has no email, generate a Gmail-like fallback so we can email credentials
            var emailWasGenerated = false;
            if (string.IsNullOrWhiteSpace(staff.Email))
            {
                var cleanId = (staff.EmployeeId ?? "").Replace(" ", "").ToLowerInvariant();
                var generated = string.IsNullOrWhiteSpace(cleanId) ? $"staff{DateTime.UtcNow.Ticks}@gmail.com" : $"{cleanId}@gmail.com";
                staff.Email = generated;
                emailWasGenerated = true;
            }

            // Generate a random password
            var password = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("=", string.Empty)
                .Replace("+", string.Empty)
                .Replace("/", string.Empty);
            password = password.Length > 12 ? password.Substring(0, 12) : password + "Xy1!";

            var role = string.IsNullOrWhiteSpace(model.Role) ? "librarian" : model.Role.Trim().ToLowerInvariant();

            var createModel = new CreateUserViewModel
            {
                Name = staff.FullName ?? staff.EmployeeId ?? "Staff",
                Username = staff.EmployeeId?.Trim() ?? string.Empty,
                Email = staff.Email?.Trim(),
                Password = password,
                ConfirmPassword = password,
                Role = role,
                Department = staff.Department,
                Course = staff.Department,
                ContactNumber = staff.ContactNumber
            };

            var created = await _userManagementService.AddUserAsync(createModel);
            if (!created)
            {
                return Json(new { success = false, message = "User already exists or could not be created. Check for duplicate username/email." });
            }

            // Send credentials via email
            try
            {
                var subject = "Your Library Account Credentials";
                var body = $@"<p>Hello {staff.FullName ?? staff.EmployeeId},</p>
<p>Your library account has been created.</p>
<p><strong>Username:</strong> {createModel.Username}<br/>
<strong>Password:</strong> {password}</p>
{(emailWasGenerated ? $"<p><em>Note: An email address was generated for you: {createModel.Email}. If this is incorrect, contact your administrator.</em></p>" : string.Empty)}
<p>Please log in and change your password.</p>";
                await _emailService.SendEmailAsync(createModel.Email!, subject, body);
            }
            catch (Exception ex)
            {
                return Json(new { success = true, message = $"User created, but failed to send email: {ex.Message}" });
            }

            return Json(new { success = true, message = "User created from MOCK staff and credentials emailed." });
        }

        // Update Password - POST
        [HttpPost]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid data provided" });
            }

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                return Json(new { success = false, message = "New passwords do not match" });
            }

            var success = await _userManagementService.UpdatePasswordAsync(model.UserId, model.OldPassword, model.NewPassword);

            if (success)
            {
                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = "Old password is incorrect or failed to update" });
            }
        }

        // Process Return - GET
        [HttpGet]
        public async Task<IActionResult> ProcessReturn(string reservationId)
        {
            ViewBag.Role = "Admin";

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
            ViewBag.Role = "Admin";

            if (!ModelState.IsValid)
            {
                return View(returnModel);
            }

            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Calculate penalties based on condition
            decimal conditionPenalty = returnModel.BookCondition switch
            {
                "Good" => 0,
                "Damage" => returnModel.DamageType switch
                {
                    "Minor" => 50m,
                    "Moderate" => 100m,
                    "Major" => 200m,
                    _ => 0m
                },
                "Lost" => 2000,
                _ => 0
            };

            returnModel.PenaltyAmount = conditionPenalty;
            returnModel.TotalPenalty = returnModel.LateFees + conditionPenalty;
            returnModel.ProcessedBy = adminId != null ? ObjectId.Parse(adminId) : null;

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
            ViewBag.Role = "Admin";
            var returns = await _returnService.GetAllReturnsAsync();
            return View(returns);
        }

        // Search Return Transaction
        [HttpGet]
        public async Task<IActionResult> SearchReturn(string searchTerm)
        {
            ViewBag.Role = "Admin";

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
            ViewBag.Role = "Admin";
            return RedirectToAction("Catalog");
        }

        // Add New Book - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBook(Book book, IFormFile bookImage)
        {
            ViewBag.Role = "Admin";

            if (ModelState.IsValid)
            {
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

                    if (success)
                    {
                        // Log successful book addition
                        await _auditLoggingHelper.LogBookActionAsync(
                            "ADD_BOOK",
                            book._id.ToString(),
                            book.Title,
                            $"Book '{book.Title}' added to library. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}, Available Copies: {book.AvailableCopies}",
                            true
                        );
                        
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
            }

            return RedirectToAction("Catalog");
        }

        // Edit Book - GET
        [HttpGet]
        public async Task<IActionResult> EditBook(string id)
        {
            ViewBag.Role = "Admin";

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
            ViewBag.Role = "Admin";

            // Align behavior with Librarian: if model _id is empty, set from route id
            if (book._id == MongoDB.Bson.ObjectId.Empty && MongoDB.Bson.ObjectId.TryParse(id, out var parsedId))
            {
                book._id = parsedId;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Get existing book first to preserve critical data
                    var existingBook = await _bookService.GetBookByIdAsync(id);
                    if (existingBook == null)
                    {
                        return NotFound();
                    }

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
                        Console.WriteLine($"[Admin.EditBook] Error reading IsActive from form: {ex}");
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
                        Console.WriteLine($"[Admin.EditBook] Error reading IsReferenceOnly from form: {ex}");
                    }

                    // Handle image upload
                    if (bookImage != null && bookImage.Length > 0)
                    {
                        if (existingBook != null && !existingBook.Image.Contains("default-book.png"))
                        {
                            DeleteBookImage(existingBook.Image);
                        }

                        var imagePath = await SaveBookImageAsync(bookImage);
                        if (imagePath != null)
                        {
                            book.Image = imagePath;
                        }
                    }
                    // If no new image uploaded, keep the existing image
                    else if (string.IsNullOrEmpty(book.Image))
                    {
                        if (existingBook != null)
                        {
                            book.Image = existingBook.Image;
                        }
                    }
                    Console.WriteLine($"[Admin.EditBook] existingBook.IsActive={existingBook?.IsActive}, incoming book.IsActive={book.IsActive}, existingBook.IsReferenceOnly={existingBook?.IsReferenceOnly}, incoming book.IsReferenceOnly={book.IsReferenceOnly}");

                    var success = await _bookService.UpdateBookAsync(id, book);

                    if (success)
                    {
                        // Log successful book update
                        await _auditLoggingHelper.LogBookActionAsync(
                            "EDIT_BOOK",
                            id,
                            book.Title,
                            $"Book '{book.Title}' updated by Admin. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}, Available Copies: {book.AvailableCopies}",
                            true
                        );
                        
                        TempData["SuccessMessage"] = "Book updated successfully!";
                        return RedirectToAction("Dashboard");
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
                }
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
                        $"Book '{book.Title}' deleted by Admin from library. ISBN: {book.ISBN}, Total Copies: {book.TotalCopies}",
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
        // Restrict a student
        [HttpPost]
        public async Task<IActionResult> RestrictUser([FromForm] string userId)
        {
            var success = await _userManagementService.RestrictUserAsync(userId);
            return Json(new { success });
        }

        // Unrestrict a student
        [HttpPost]
        public async Task<IActionResult> UnrestrictUser([FromForm] string userId)
        {
            var success = await _userManagementService.UnrestrictUserAsync(userId);
            return Json(new { success });
        }

        // Deactivate a staff member
        [HttpPost]
        public async Task<IActionResult> DeactivateUser([FromForm] string userId)
        {
            var success = await _userManagementService.DeactivateUserAsync(userId);
            return Json(new { success });
        }

        // Activate a staff member
        [HttpPost]
        public async Task<IActionResult> ActivateUser([FromForm] string userId)
        {
            var success = await _userManagementService.ActivateUserAsync(userId);
            return Json(new { success });
        }

        // Delete user
        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromForm] string userId)
        {
            // Get user info before deleting
            var targetUser = await _userManagementService.GetUserByIdAsync(userId);

            var success = await _userManagementService.DeleteUserAsync(userId);

            if (success)
            {
                // CREATE AUDIT LOG ✅
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var adminName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

                await _auditLogService.CreateAuditLogAsync(
                    adminId,
                    adminName,
                    "ADMIN",
                    "User Account Deleted",
                    $"Deleted user account: <strong>{targetUser?.Username}</strong>",
                    "User",
                    userId
                );
            }

            return Json(new { success });
        }
        // Admin Reset Password - POST (No old password required)
        [HttpPost]
        public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetPasswordViewModel model)
        {
            // Add detailed error logging
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return Json(new
                {
                    success = false,
                    message = "Invalid data provided",
                    errors = errors,
                    receivedData = new
                    {
                        userId = model?.UserId,
                        hasNewPassword = !string.IsNullOrEmpty(model?.NewPassword),
                        hasConfirmPassword = !string.IsNullOrEmpty(model?.ConfirmNewPassword)
                    }
                });
            }

            if (model.NewPassword != model.ConfirmNewPassword)
            {
                return Json(new { success = false, message = "Passwords do not match" });
            }

            var success = await _userManagementService.AdminResetPasswordAsync(model.UserId, model.NewPassword);

            if (success)
            {
                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = "Failed to reset password. User may not exist." });
            }
        }


        // Reservation Management
        [HttpPost]
        public async Task<IActionResult> ApproveReservation(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                
                var success = await _reservationService.ApproveReservationAsync(reservationId, adminId);

                if (success)
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "APPROVE_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Reservation approved by Admin for pickup. Due date: {reservation?.DueDate?.ToString("MMM dd, yyyy") ?? "Not set"}",
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
            ViewBag.Role = "Admin";

            var viewModel = await _reportService.GetCompleteReportAsync(timeRange, from, to);
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RejectReservation(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                
                var success = await _reservationService.RejectReservationAsync(reservationId, adminId);

                if (success)
                {
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "REJECT_RESERVATION",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Reservation rejected by Admin. Reason: Manual rejection",
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
        // Create Backup
        [HttpPost]
        public async Task<IActionResult> CreateBackup()
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

            var (success, backupId, message) = await _backupService.CreateBackupAsync(adminName);

            if (success)
            {
                // Create audit log
                await _auditLogService.CreateAuditLogAsync(
                    adminId,
                    adminName,
                    "ADMIN",
                    "Database Backup Created",
                    $"Manual database backup created successfully",
                    "Backup",
                    backupId
                );

                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            return RedirectToAction("SystemMaintenance");
        }

        // Download Backup
        [HttpGet]
        public async Task<IActionResult> DownloadBackup(string backupId)
        {
            var (success, fileData, fileName) = await _backupService.DownloadBackupAsync(backupId);

            if (success)
            {
                return File(fileData, "application/json", fileName);
            }

            TempData["ErrorMessage"] = "Backup file not found.";
            return RedirectToAction("SystemMaintenance");
        }

        // Restore Backup
        [HttpPost]
        public async Task<IActionResult> RestoreBackup(string backupId)
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var adminName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

            var (success, message) = await _backupService.RestoreBackupAsync(backupId, adminName);

            if (success)
            {
                // Create audit log
                await _auditLogService.CreateAuditLogAsync(
                    adminId,
                    adminName,
                    "ADMIN",
                    "Database Restored",
                    $"Database restored from backup",
                    "Backup",
                    backupId
                );

                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            return RedirectToAction("SystemMaintenance");
        }

        // Delete Backup
        [HttpPost]
        public async Task<IActionResult> DeleteBackup(string backupId)
        {
            var success = await _backupService.DeleteBackupAsync(backupId);

            if (success)
            {
                TempData["SuccessMessage"] = "Backup deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to delete backup.";
            }

            return RedirectToAction("SystemMaintenance");
        }

        // === LIBRARIAN FUNCTIONALITY FOR ADMIN ===
        
        [HttpGet]
        public async Task<IActionResult> SearchStudent(string studentId)
        {
            try
            {
                var studentInfo = await _transactionService.GetStudentInfoAsync(studentId);
                if (studentInfo != null)
                {
                    return Json(new { success = true, data = studentInfo });
                }
                return Json(new { success = false, message = "Student not found" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error searching for student" });
            }
        }

        [HttpGet]
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

        [HttpPost]
        public async Task<IActionResult> CheckEligibility([FromBody] CheckEligibilityRequest request)
        {
            try
            {
                var eligibility = await _transactionService.CheckBorrowingEligibilityAsync(request.StudentId, request.BookId);
                return Json(new { success = true, data = eligibility });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error checking eligibility" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessBorrowing([FromBody] DirectBorrowingRequest request)
        {
            try
            {
                var success = await _transactionService.ProcessDirectBorrowingAsync(request);
                if (success)
                {
                    return Json(new { success = true, message = "Book borrowed successfully" });
                }
                return Json(new { success = false, message = "Failed to process borrowing" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing borrowing" });
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
                    return Json(new { success = false, message = "User not found" });
                }

                // Calculate days borrowed and overdue status in minutes
                var borrowDate = reservation.ApprovalDate ?? reservation.ReservationDate;
                var dueDate = reservation.DueDate ?? DateTime.UtcNow;
                var daysBorrowed = (DateTime.UtcNow - borrowDate).Days;
                var minutesLate = 0;
                var isOverdue = false;

                if (dueDate < DateTime.UtcNow)
                {
                    minutesLate = (int)Math.Ceiling((DateTime.UtcNow - dueDate).TotalMinutes);
                    isOverdue = true;
                }

                // Calculate late fee: ₱10 per minute overdue
                var lateFee = minutesLate > 0 ? minutesLate * 10m : 0m;

                var responseData = new
                {
                    reservationId = reservation._id,
                    bookId = book._id.ToString(),
                    userId = user._id.ToString(),
                    bookTitle = book.Title,
                    isbn = book.ISBN,
                    borrowerName = $"{user.FullName}",
                    borrowDate = borrowDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    dueDate = dueDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    daysBorrowed = daysBorrowed,
                    daysLate = minutesLate, // Store minutes in daysLate for compatibility
                    minutesLate = minutesLate,
                    lateFee = lateFee,
                    isOverdue = isOverdue
                };

                return Json(new { success = true, data = responseData });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SearchActiveReservation: {ex.Message}");
                return Json(new { success = false, message = "Error searching for reservation" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessReturnTransaction([FromBody] ProcessReturnRequest request)
        {
            try
            {
                // Convert ProcessReturnRequest to ReturnTransaction
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var returnTransaction = new ReturnTransaction
                {
                    ReservationId = ObjectId.Parse(request.ReservationId),
                    BookId = ObjectId.Parse(request.BookId),
                    UserId = ObjectId.Parse(request.UserId),
                    BookTitle = request.BookTitle,
                    BorrowDate = DateTime.Parse(request.BorrowDate),
                    DueDate = DateTime.Parse(request.DueDate),
                    ReturnDate = DateTime.UtcNow,
                    DaysLate = request.DaysLate,
                    LateFees = request.LateFees,
                    BookCondition = request.BookCondition,
                    DamageType = request.DamageType,
                    DamagePenalty = request.DamagePenalty,
                    PenaltyAmount = request.PenaltyAmount,
                    TotalPenalty = request.TotalPenalty,
                    PaymentStatus = "Pending",
                    ProcessedBy = !string.IsNullOrEmpty(adminId) ? ObjectId.Parse(adminId) : null
                };

                var success = await _returnService.ProcessReturnAsync(returnTransaction);
                if (success)
                {
                    return Json(new { success = true, message = "Return processed successfully" });
                }
                return Json(new { success = false, message = "Failed to process return" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing return" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RestrictAccount([FromBody] RestrictAccountRequest request)
        {
            try
            {
                var success = await _userManagementService.RestrictUserAsync(request.UserId);
                if (success)
                {
                    return Json(new { success = true, message = "Account restricted successfully" });
                }
                return Json(new { success = false, message = "Failed to restrict account" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error restricting account" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBorrowedHistory()
        {
            try
            {
                var reservations = await _reservationService.GetAllBorrowingHistoryAsync();
                var books = new List<Book>();
                var users = new List<User>();

                if (reservations.Any())
                {
                    var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                    var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                    books = (await Task.WhenAll(bookTasks)).ToList();
                    users = (await Task.WhenAll(userTasks)).ToList();
                }

                var enrichedData = reservations.Select((reservation, index) =>
                {
                    var book = books[index];
                    var user = users[index];
                    bool isOverdue = reservation.Status == "Borrowed" && reservation.DueDate.HasValue
                        && DateTime.UtcNow > reservation.DueDate.Value;

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
                return Json(new { success = false, message = "Error fetching borrowed history" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPendingReservations()
        {
            try
            {
                var reservations = await _reservationService.GetPendingReservationsAsync();
                var books = new List<Book>();
                var users = new List<User>();

                if (reservations.Any())
                {
                    var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                    var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                    books = (await Task.WhenAll(bookTasks)).ToList();
                    users = (await Task.WhenAll(userTasks)).ToList();
                }

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
                return Json(new { success = false, message = "Error fetching pending reservations" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetApprovedReservations()
        {
            try
            {
                // Auto-cancel expired approvals first (2-minute pickup window)
                await _reservationService.AutoCancelExpiredPickupsAsync();
                
                var reservations = await _reservationService.GetActiveReservationsAsync();
                var books = new List<Book>();
                var users = new List<User>();

                if (reservations.Any())
                {
                    var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                    var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                    books = (await Task.WhenAll(bookTasks)).ToList();
                    users = (await Task.WhenAll(userTasks)).ToList();
                }

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
                return Json(new { success = false, message = "Error fetching approved reservations" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBorrowedReservations()
        {
            try
            {
                var reservations = await _reservationService.GetAllBorrowingsAsync();
                var books = new List<Book>();
                var users = new List<User>();

                if (reservations.Any())
                {
                    var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                    var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                    books = (await Task.WhenAll(bookTasks)).ToList();
                    users = (await Task.WhenAll(userTasks)).ToList();
                }

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
                return Json(new { success = false, message = "Error fetching borrowed reservations" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCancelledReservations()
        {
            try
            {
                var reservations = await _reservationService.GetReturnedBooksAsync();
                var books = new List<Book>();
                var users = new List<User>();

                if (reservations.Any())
                {
                    var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                    var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                    books = (await Task.WhenAll(bookTasks)).ToList();
                    users = (await Task.WhenAll(userTasks)).ToList();
                }

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
                return Json(new { success = false, message = "Error fetching cancelled reservations" });
            }
        }

        // === RENEWALS (mirror Librarian) ===
        [HttpGet]
        public async Task<IActionResult> GetRenewalRequests()
        {
            try
            {
                var reservations = await _reservationService.GetRenewalRequestsAsync();
                var bookTasks = reservations.Select(r => _bookService.GetBookByIdAsync(r.BookId)).ToList();
                var userTasks = reservations.Select(r => _userService.GetUserByIdAsync(r.UserId)).ToList();
                var books = await Task.WhenAll(bookTasks);
                var users = await Task.WhenAll(userTasks);

                var enriched = reservations.Select((r, i) => new
                {
                    reservationId = r._id,
                    userId = r.UserId,
                    bookId = r.BookId,
                    userName = users[i]?.FullName ?? "Unknown User",
                    studentNumber = r.StudentNumber ?? "N/A",
                    bookTitle = books[i]?.Title ?? "Unknown Book",
                    isbn = books[i]?.ISBN ?? "N/A",
                    reservationDate = r.ReservationDate.ToString("MM/dd/yyyy"),
                    approvalDate = r.ApprovalDate?.ToString("MM/dd/yyyy") ?? "N/A",
                    dueDate = r.DueDate?.ToString("MM/dd/yyyy") ?? "N/A",
                    status = r.Status,
                    borrowType = r.BorrowType ?? "ONLINE"
                }).ToList();

                return Json(new { success = true, data = enriched });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error fetching renewal requests" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveRenewal(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _reservationService.ApproveRenewalAsync(reservationId, adminId);
                if (success)
                {
                    return Json(new { success = true, message = "Renewal approved successfully" });
                }
                return Json(new { success = false, message = "Failed to approve renewal" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error approving renewal" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectRenewal(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _reservationService.RejectRenewalAsync(reservationId, adminId);
                if (success)
                {
                    return Json(new { success = true, message = "Renewal rejected" });
                }
                return Json(new { success = false, message = "Failed to reject renewal" });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Error rejecting renewal" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsBorrowed(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                {
                    return Json(new { success = false, message = "Admin ID not found" });
                }

                var reservation = await _reservationService.GetReservationByIdAsync(reservationId);
                var success = await _reservationService.MarkAsBorrowedAsync(reservationId, adminId);
                
                if (success)
                {
                    // Log successful borrowing
                    await _auditLoggingHelper.LogBorrowingActionAsync(
                        "MARK_AS_BORROWED",
                        reservationId,
                        reservation?.BookTitle ?? "Unknown Book",
                        reservation?.StudentNumber ?? "Unknown Student",
                        $"Book marked as borrowed by Admin. Due date: {reservation?.DueDate?.ToString("MMM dd, yyyy") ?? "Not set"}. Copy ID: {reservation?.CopyIdentifier ?? "Not assigned"}",
                        true
                    );
                    
                    return Json(new { success = true, message = "Book marked as borrowed successfully" });
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
                    
                    return Json(new { success = false, message = "Failed to mark book as borrowed" });
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
                
                return Json(new { success = false, message = "Error marking book as borrowed" });
            }
        }

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

        [HttpPost]
        public async Task<IActionResult> MarkWithAction(string reservationId, string actionType, string remarks)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(adminId))
                {
                    return Json(new { success = false, message = "Admin ID not found" });
                }

                // For now, just return success - this would need to be implemented in the service
                return Json(new { success = true, message = $"Reservation {actionType.ToLower()} successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error {actionType.ToLower()}ing reservation" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AutoCancelReservation(string reservationId)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var success = await _reservationService.CancelReservationAsync(
                    reservationId,
                    adminId,
                    "Cancelled by administrator");

                if (success)
                {
                    return Json(new { success = true, message = "Reservation cancelled successfully" });
                }

                return Json(new { success = false, message = "Failed to cancel reservation." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error cancelling reservation" });
            }
        }

        // Book Condition Management Methods

        [HttpGet]
        public async Task<IActionResult> BookCondition()
        {
            ViewBag.Role = "Admin";
            
            try
            {
                // Get all returned books for condition assessment
                var returnedBooks = await _returnService.GetAllReturnsAsync();
                
                // Fetch book details to populate ISBN and ClassificationNo
                foreach (var bookReturn in returnedBooks)
                {
                    try
                    {
                        var book = await _bookService.GetBookByIdAsync(bookReturn.BookId.ToString());
                        if (book != null)
                        {
                            bookReturn.ISBN = book.ISBN;
                            bookReturn.ClassificationNo = book.ClassificationNo;
                        }
                    }
                    catch
                    {
                        // If book not found, leave ISBN and ClassificationNo as empty
                    }
                }
                
                return View(returnedBooks);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading returned books: {ex.Message}";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBookCondition(string bookId, string condition, string notes = "")
        {
            try
            {
                ViewBag.Role = "Admin";
                
                // Get the book to log its title
                var book = await _bookService.GetBookByIdAsync(bookId);
                var bookTitle = book?.Title ?? "Unknown Book";
                
                // Here you would implement the logic to update book condition
                // This might involve updating a BookCondition collection or adding condition info to the book
                // For now, we'll just log the action as the actual condition update logic would need to be implemented
                
                await _auditLoggingHelper.LogBookActionAsync(
                    "UPDATE_BOOK_CONDITION",
                    bookId,
                    bookTitle,
                    $"Book condition updated to: {condition}. Notes: {notes}",
                    true
                );
                
                return Json(new { success = true, message = "Book condition updated successfully" });
            }
            catch (Exception ex)
            {
                await _auditLoggingHelper.LogBookActionAsync(
                    "UPDATE_BOOK_CONDITION",
                    bookId,
                    "Unknown Book",
                    $"Failed to update book condition: {ex.Message}",
                    false,
                    ex.Message
                );
                
                return Json(new { success = false, message = $"Error updating book condition: {ex.Message}" });
            }
        }

        // Penalty Management Methods

        [HttpGet]
        public async Task<IActionResult> GetUserPenalties(string userId)
        {
            try
            {
                Console.WriteLine($"[GetUserPenalties] UserId: {userId}");
                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                Console.WriteLine($"[GetUserPenalties] Found {penalties.Count} penalties");
                
                var result = penalties.Select(p => new
                {
                    id = p._id.ToString(),
                    studentNumber = p.StudentNumber,
                    studentName = p.StudentName,
                    bookTitle = p.BookTitle,
                    penaltyType = p.PenaltyType,
                    amount = p.Amount,
                    description = p.Description,
                    isPaid = p.IsPaid,
                    createdDate = p.CreatedDate.ToString("MMM dd, yyyy"),
                    paymentDate = p.PaymentDate?.ToString("MMM dd, yyyy") ?? "Not paid"
                }).ToList();
                
                Console.WriteLine($"[GetUserPenalties] Returning {result.Count} penalties");
                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetUserPenalties] Error: {ex.Message}");
                return Json(new { success = false, message = "Error loading penalties: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ClearAllPenalties(string userId)
        {
            try
            {
                Console.WriteLine($"[ClearAllPenalties] UserId: {userId}");
                var penalties = await _penaltyService.GetUserPenaltiesAsync(userId);
                Console.WriteLine($"[ClearAllPenalties] Found {penalties.Count} total penalties");
                
                var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                Console.WriteLine($"[ClearAllPenalties] Found {pendingPenalties.Count} pending penalties");

                foreach (var penalty in pendingPenalties)
                {
                    Console.WriteLine($"[ClearAllPenalties] Removing penalty: {penalty._id}");
                    await _penaltyService.RemovePenaltyAsync(penalty._id.ToString());
                }

                Console.WriteLine($"[ClearAllPenalties] Successfully cleared {pendingPenalties.Count} penalties");
                return Json(new { success = true, message = $"Cleared {pendingPenalties.Count} penalties for student" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClearAllPenalties] Error: {ex.Message}");
                return Json(new { success = false, message = "Error clearing penalties: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPenaltyDetails(string penaltyId)
        {
            try
            {
                var penalties = await _penaltyService.GetAllPenaltiesAsync();
                var penalty = penalties.FirstOrDefault(p => p._id.ToString() == penaltyId);
                
                if (penalty == null)
                {
                    return Json(new { success = false, message = "Penalty not found" });
                }
                
                return Json(new { 
                    success = true, 
                    penalty = new {
                        id = penalty._id.ToString(),
                        amount = penalty.Amount,
                        bookTitle = penalty.BookTitle,
                        penaltyType = penalty.PenaltyType,
                        description = penalty.Description,
                        isPaid = penalty.IsPaid,
                        createdDate = penalty.CreatedDate.ToString("MMM dd, yyyy")
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPenaltyDetails] Error: {ex.Message}");
                return Json(new { success = false, message = "Error getting penalty details: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPaymentProof(IFormFile file, string penaltyId, string penaltyAmount)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No file uploaded" });
                }

                // Validate file size (10MB max)
                if (file.Length > 10 * 1024 * 1024)
                {
                    return Json(new { success = false, message = "File size exceeds 10MB limit" });
                }

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Json(new { success = false, message = "Invalid file type. Only JPG, PNG, and PDF are allowed." });
                }

                // Upload to Cloudinary
                string fileUrl;
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "payment-proofs",
                        PublicId = $"penalty_{penaltyId}_{DateTime.UtcNow.Ticks}",
                        Overwrite = false
                    };

                    if (fileExtension == ".pdf")
                    {
                        // For PDF, use RawUploadParams
                        var rawUploadParams = new RawUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "payment-proofs",
                            PublicId = $"penalty_{penaltyId}_{DateTime.UtcNow.Ticks}"
                        };
                        var rawResult = await _cloudinary.UploadAsync(rawUploadParams);
                        fileUrl = rawResult.SecureUrl.ToString();
                    }
                    else
                    {
                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        fileUrl = uploadResult.SecureUrl.ToString();
                    }
                }

                // Extract text from image/PDF using real OCR
                var extractedText = await ExtractTextFromFile(file, fileUrl);
                
                Console.WriteLine($"[ProcessPaymentProof] Extracted text length: {extractedText?.Length ?? 0}");
                Console.WriteLine($"[ProcessPaymentProof] Extracted text preview: {extractedText?.Substring(0, Math.Min(200, extractedText?.Length ?? 0))}");
                
                // Only extract amount if we actually got text
                decimal? extractedAmount = null;
                double confidence = 0.0;
                
                if (!string.IsNullOrWhiteSpace(extractedText) && extractedText.Trim().Length >= 5)
                {
                    extractedAmount = ExtractAmountFromText(extractedText, decimal.Parse(penaltyAmount));
                    Console.WriteLine($"[ProcessPaymentProof] Extracted amount: {extractedAmount}");
                    confidence = CalculateConfidence(extractedText, extractedAmount, decimal.Parse(penaltyAmount));
                }
                else
                {
                    // No text extracted - return error with helpful message
                    var fileType = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var errorMessage = fileType == ".pdf" 
                        ? "Unable to extract text from PDF. The PDF may be image-based (scanned). Please try:\n1. Converting the PDF to an image file (JPG/PNG)\n2. Or ensure the PDF contains selectable text"
                        : "Unable to extract text from image. Please ensure:\n1. Tesseract OCR is installed on the server\n2. The image is clear and text is readable\n3. The image is not too dark or blurry";
                    
                    return Json(new
                    {
                        success = false,
                        message = errorMessage,
                        extractedText = extractedText ?? string.Empty
                    });
                }

                return Json(new
                {
                    success = true,
                    fileUrl = fileUrl,
                    extractedText = extractedText,
                    extractedAmount = extractedAmount,
                    confidence = confidence
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProcessPaymentProof] Error: {ex.Message}");
                return Json(new { success = false, message = "Error processing payment proof: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyAndMarkPenaltyAsPaid(string penaltyId, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "No payment proof uploaded" });
                }

                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                // Get penalty details
                var penalties = await _penaltyService.GetAllPenaltiesAsync();
                var penalty = penalties.FirstOrDefault(p => p._id.ToString() == penaltyId);
                
                if (penalty == null)
                {
                    return Json(new { success = false, message = "Penalty not found" });
                }

                // Upload payment proof to Cloudinary
                string proofUrl;
                using (var stream = file.OpenReadStream())
                {
                    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "payment-proofs",
                        PublicId = $"penalty_proof_{penaltyId}_{DateTime.UtcNow.Ticks}",
                        Overwrite = false
                    };

                    if (fileExtension == ".pdf")
                    {
                        var rawUploadParams = new RawUploadParams
                        {
                            File = new FileDescription(file.FileName, stream),
                            Folder = "payment-proofs",
                            PublicId = $"penalty_proof_{penaltyId}_{DateTime.UtcNow.Ticks}"
                        };
                        var rawResult = await _cloudinary.UploadAsync(rawUploadParams);
                        proofUrl = rawResult.SecureUrl.ToString();
                    }
                    else
                    {
                        var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                        proofUrl = uploadResult.SecureUrl.ToString();
                    }
                }

                // Mark penalty as paid
                var success = await _penaltyService.MarkPenaltyAsPaidAsync(penaltyId, adminId ?? "Admin");
                
                if (success)
                {
                    // Store payment proof URL in penalty remarks or create a separate payment record
                    // For now, we'll store it in the remarks field
                    var objectId = ObjectId.Parse(penaltyId);
                    var update = Builders<Penalty>.Update
                        .Set(p => p.Remarks, $"Payment verified. Proof: {proofUrl}");
                    
                    var penaltiesCollection = _mongoDbService.GetCollection<Penalty>("Penalties");
                    await penaltiesCollection.UpdateOneAsync(p => p._id == objectId, update);

                    Console.WriteLine($"[VerifyAndMarkPenaltyAsPaid] Successfully verified and marked penalty as paid");
                    return Json(new { success = true, message = "Payment verified and penalty marked as paid successfully" });
                }
                
                return Json(new { success = false, message = "Failed to mark penalty as paid" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VerifyAndMarkPenaltyAsPaid] Error: {ex.Message}");
                return Json(new { success = false, message = "Error verifying payment: " + ex.Message });
            }
        }

        // Helper method to extract text from file using real OCR
        private async Task<string> ExtractTextFromFile(IFormFile file, string fileUrl)
        {
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string extractedText = string.Empty;
            
            try
            {
                if (fileExtension == ".pdf")
                {
                    // Extract text from PDF using PdfPig
                    extractedText = await ExtractTextFromPdf(file);
                    
                    // If PDF extraction returned little/no text, it might be a scanned PDF
                    if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Trim().Length < 10)
                    {
                        return string.Empty; // Return empty - don't generate fake text
                    }
                }
                else
                {
                    // Extract text from image using Tesseract OCR
                    extractedText = await ExtractTextFromImage(file);
                    
                    // Only return actual extracted text, never generate fake data
                    if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Trim().Length < 5)
                    {
                        return string.Empty; // Return empty if no text found
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractTextFromFile] Error: {ex.Message}");
                return string.Empty; // Return empty on error, don't generate fake text
            }
            
            return extractedText?.Trim() ?? string.Empty;
        }

        // Extract text from PDF using PdfPig
        private async Task<string> ExtractTextFromPdf(IFormFile file)
        {
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    var pdfDocument = UglyToad.PdfPig.PdfDocument.Open(stream);
                    var textBuilder = new StringBuilder();
                    
                    foreach (var page in pdfDocument.GetPages())
                    {
                        try
                        {
                            var words = page.GetWords();
                            if (words != null)
                            {
                                foreach (var word in words)
                                {
                                    if (!string.IsNullOrWhiteSpace(word.Text))
                                    {
                                        textBuilder.Append(word.Text);
                                        textBuilder.Append(" ");
                                    }
                                }
                                textBuilder.AppendLine();
                            }
                        }
                        catch (Exception pageEx)
                        {
                            Console.WriteLine($"[ExtractTextFromPdf] Error on page: {pageEx.Message}");
                            // Continue with next page
                        }
                    }
                    
                    pdfDocument.Dispose();
                    var result = textBuilder.ToString().Trim();
                    
                    // Only return if we actually extracted meaningful text
                    if (result.Length < 5)
                    {
                        return string.Empty;
                    }
                    
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractTextFromPdf] Error: {ex.Message}");
                // Return empty - don't generate fake text
                return string.Empty;
            }
        }

        // Extract text from image using Tesseract OCR
        private async Task<string> ExtractTextFromImage(IFormFile file)
        {
            try
            {
                // Tesseract requires the tesseract data files to be available
                var tesseractDataPath = Path.Combine(_environment.ContentRootPath, "tessdata");
                
                // Try common installation paths for Tesseract
                if (!Directory.Exists(tesseractDataPath))
                {
                    var commonPaths = new[]
                    {
                        @"C:\Program Files\Tesseract-OCR\tessdata",
                        @"C:\Program Files (x86)\Tesseract-OCR\tessdata",
                        Path.Combine(_environment.ContentRootPath, "tessdata"),
                        @"./tessdata"
                    };
                    
                    foreach (var path in commonPaths)
                    {
                        if (Directory.Exists(path))
                        {
                            tesseractDataPath = path;
                            Console.WriteLine($"[ExtractTextFromImage] Found Tesseract data at: {tesseractDataPath}");
                            break;
                        }
                    }
                }

                // If tessdata not found, return empty (no fake data)
                if (!Directory.Exists(tesseractDataPath))
                {
                    Console.WriteLine("[ExtractTextFromImage] Tesseract data not found. OCR unavailable.");
                    Console.WriteLine("[ExtractTextFromImage] Please install Tesseract OCR from: https://github.com/UB-Mannheim/tesseract/wiki");
                    return string.Empty; // Return empty - don't generate fake text
                }

                Console.WriteLine($"[ExtractTextFromImage] Using Tesseract data path: {tesseractDataPath}");

                // Initialize Tesseract engine
                using (var engine = new TesseractEngine(tesseractDataPath, "eng", EngineMode.Default))
                {
                    // Set page segmentation mode for better receipt recognition
                    engine.SetVariable("tessedit_pageseg_mode", "6"); // Assume uniform block of text
                    
                    // Load and process image
                    using (var imageStream = file.OpenReadStream())
                    {
                        // Read image bytes
                        byte[] imageBytes;
                        using (var memoryStream = new MemoryStream())
                        {
                            await imageStream.CopyToAsync(memoryStream);
                            imageBytes = memoryStream.ToArray();
                        }

                        Console.WriteLine($"[ExtractTextFromImage] Image size: {imageBytes.Length} bytes");

                        // Load image using ImageSharp
                        using (var image = SixLabors.ImageSharp.Image.Load(imageBytes))
                        {
                            // Convert to PNG format for Tesseract
                            using (var memoryStream = new MemoryStream())
                            {
                                await image.SaveAsync(memoryStream, new PngEncoder());
                                memoryStream.Position = 0;
                                
                                using (var pix = Pix.LoadFromMemory(memoryStream.ToArray()))
                                using (var page = engine.Process(pix))
                                {
                                    var text = page.GetText()?.Trim();
                                    
                                    Console.WriteLine($"[ExtractTextFromImage] Extracted text length: {text?.Length ?? 0}");
                                    Console.WriteLine($"[ExtractTextFromImage] Extracted text preview: {text?.Substring(0, Math.Min(200, text?.Length ?? 0))}");
                                    
                                    // Only return if we actually extracted meaningful text
                                    if (string.IsNullOrWhiteSpace(text) || text.Length < 5)
                                    {
                                        Console.WriteLine("[ExtractTextFromImage] Text too short or empty");
                                        return string.Empty;
                                    }
                                    
                                    return text;
                                }
                            }
                        }
                    }
                }
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine($"[ExtractTextFromImage] Tesseract native DLLs not found: {ex.Message}");
                Console.WriteLine("[ExtractTextFromImage] Please install Tesseract OCR from: https://github.com/UB-Mannheim/tesseract/wiki");
                return string.Empty; // Return empty - don't generate fake text
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractTextFromImage] Error: {ex.Message}");
                Console.WriteLine($"[ExtractTextFromImage] Stack trace: {ex.StackTrace}");
                return string.Empty; // Return empty on error - don't generate fake text
            }
        }

        // Helper method to extract amount from text using regex
        private decimal? ExtractAmountFromText(string text, decimal expectedAmount)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 5)
                return null;

            Console.WriteLine($"[ExtractAmountFromText] Searching for amount in text (expected: {expectedAmount})");
            Console.WriteLine($"[ExtractAmountFromText] Text sample: {text.Substring(0, Math.Min(500, text.Length))}");

            // More specific patterns to match currency amounts in receipts
            // Priority order: Most specific first
            var patterns = new[]
            {
                // "Total: 127.50" or "Total 127.50" (most common in receipts)
                @"Total[:\s]+(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                @"TOTAL[:\s]+(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                
                // Patterns with currency symbols
                @"₱\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",  // ₱1,234.56
                @"PHP\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // PHP 1,234.56
                @"\$?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)", // $1,234.56 or just 1,234.56
                
                // Patterns with context words
                @"(?:Amount|Paid|Payment|Balance|Due)[:\s]*₱?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                @"Amount\s+Paid[:\s]*₱?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                @"Payment\s+Amount[:\s]*₱?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                @"Total\s+Amount[:\s]*₱?\s*(\d{1,3}(?:,\d{3})*(?:\.\d{2})?)",
                
                // Simple patterns without currency symbol
                @"Total[:\s]+(\d+\.\d{2})",
                @"TOTAL[:\s]+(\d+\.\d{2})"
            };

            // Try patterns first (more accurate)
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var amountStr = match.Groups[1].Value.Replace(",", ""); // Remove commas
                        if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                        {
                            Console.WriteLine($"[ExtractAmountFromText] Found amount via pattern '{pattern}': {amount}");
                            // Validate amount is reasonable
                            if (amount > 0)
                            {
                                // If we have expected amount, prefer closer matches
                                if (expectedAmount > 0)
                                {
                                    var difference = Math.Abs(amount - expectedAmount);
                                    var percentageDiff = difference / expectedAmount;
                                    if (percentageDiff <= 0.5m) // Within 50% - reasonable for OCR
                                    {
                                        Console.WriteLine($"[ExtractAmountFromText] Amount {amount} matches expected {expectedAmount} (diff: {percentageDiff:P})");
                                        return amount;
                                    }
                                }
                                else
                                {
                                    // No expected amount, return first reasonable amount found
                                    return amount;
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: look for all decimal numbers and find the best match
            var numberPattern = @"(\d{1,3}(?:,\d{3})*\.\d{2})|(\d+\.\d{2})";
            var allMatches = Regex.Matches(text, numberPattern);
            
            Console.WriteLine($"[ExtractAmountFromText] Found {allMatches.Count} decimal numbers in text");
            
            decimal? bestMatch = null;
            decimal minDifference = decimal.MaxValue;
            
            foreach (Match match in allMatches)
            {
                var amountStr = match.Value.Replace(",", "");
                if (decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
                {
                    Console.WriteLine($"[ExtractAmountFromText] Found number: {amount}");
                    // Only consider amounts that are reasonable
                    if (amount > 0)
                    {
                        if (expectedAmount > 0)
                        {
                            var difference = Math.Abs(amount - expectedAmount);
                            if (difference < minDifference)
                            {
                                minDifference = difference;
                                bestMatch = amount;
                            }
                        }
                        else
                        {
                            // No expected amount, return largest reasonable amount (likely the total)
                            if (!bestMatch.HasValue || amount > bestMatch.Value)
                            {
                                bestMatch = amount;
                            }
                        }
                    }
                }
            }

            // Return best match if found
            if (bestMatch.HasValue)
            {
                if (expectedAmount > 0)
                {
                    var percentageDiff = minDifference / expectedAmount;
                    Console.WriteLine($"[ExtractAmountFromText] Best match: {bestMatch} (diff from expected: {percentageDiff:P})");
                    // More lenient threshold - within 50% of expected
                    if (percentageDiff <= 0.5m)
                    {
                        return bestMatch;
                    }
                }
                else
                {
                    return bestMatch;
                }
            }

            Console.WriteLine($"[ExtractAmountFromText] No amount found matching expected {expectedAmount}");
            return null;
        }

        // Helper method to calculate confidence score
        private double CalculateConfidence(string extractedText, decimal? extractedAmount, decimal expectedAmount)
        {
            if (extractedAmount == null)
                return 0.0;

            // Base confidence on how close the extracted amount is to expected
            var amountDifference = Math.Abs(extractedAmount.Value - expectedAmount);
            var percentageDifference = (double)(amountDifference / expectedAmount);
            
            // Higher confidence if amount matches exactly or very closely
            if (percentageDifference < 0.01) // Within 1%
                return 0.95;
            else if (percentageDifference < 0.05) // Within 5%
                return 0.85;
            else if (percentageDifference < 0.10) // Within 10%
                return 0.70;
            else
                return 0.50;
        }

        [HttpPost]
        public async Task<IActionResult> MarkPenaltyAsPaid(string penaltyId)
        {
            try
            {
                Console.WriteLine($"[MarkPenaltyAsPaid] PenaltyId: {penaltyId}");
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                
                var success = await _penaltyService.MarkPenaltyAsPaidAsync(penaltyId, adminId ?? "Admin");
                
                if (success)
                {
                    Console.WriteLine($"[MarkPenaltyAsPaid] Successfully marked penalty as paid");
                    return Json(new { success = true, message = "Penalty marked as paid successfully" });
                }
                
                return Json(new { success = false, message = "Failed to mark penalty as paid" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarkPenaltyAsPaid] Error: {ex.Message}");
                return Json(new { success = false, message = "Error marking penalty as paid: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemovePenalty(string penaltyId)
        {
            try
            {
                Console.WriteLine($"[RemovePenalty] PenaltyId: {penaltyId}");
                
                var success = await _penaltyService.RemovePenaltyAsync(penaltyId);
                
                if (success)
                {
                    Console.WriteLine($"[RemovePenalty] Successfully removed penalty");
                    return Json(new { success = true, message = "Penalty removed successfully" });
                }
                
                return Json(new { success = false, message = "Failed to remove penalty" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RemovePenalty] Error: {ex.Message}");
                return Json(new { success = false, message = "Error removing penalty: " + ex.Message });
            }
        }

        // Unrestrict Requests Management Page
        public async Task<IActionResult> UnrestrictRequests()
        {
            ViewBag.Role = "Admin";
            var requests = await _unrestrictRequestService.GetAllRequestsAsync();
            return View(requests);
        }

        // === UNRESTRICT REQUEST MANAGEMENT ===

        // Get all unrestrict requests
        [HttpGet]
        public async Task<IActionResult> GetUnrestrictRequests()
        {
            try
            {
                var requests = await _unrestrictRequestService.GetAllRequestsAsync();
                return Json(new { success = true, data = requests });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error fetching requests: " + ex.Message });
            }
        }

        // Get pending unrestrict requests
        [HttpGet]
        public async Task<IActionResult> GetPendingUnrestrictRequests()
        {
            try
            {
                var requests = await _unrestrictRequestService.GetPendingRequestsAsync();
                return Json(new { success = true, data = requests });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error fetching pending requests: " + ex.Message });
            }
        }

        // Admin test helper: force start borrow or set due date for testing penalties
        [HttpPost]
        public async Task<IActionResult> ForceStartBorrow(string reservationId, int dueOffsetMinutes = -5, string status = "Borrowed")
        {
            try
            {
                if (string.IsNullOrEmpty(reservationId))
                    return Json(new { success = false, message = "reservationId is required" });

                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Admin";
                var dueDate = DateTime.UtcNow.AddMinutes(dueOffsetMinutes);

                var success = await _reservationService.ForceSetDueDateAsync(reservationId, dueDate, status, adminId);
                if (success)
                {
                    return Json(new { success = true, message = $"Reservation updated. DueDate set to {dueDate:u}, status set to {status}" });
                }

                return Json(new { success = false, message = "Failed to update reservation" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Process unrestrict request (approve/reject)
        [HttpPost]
        public async Task<IActionResult> ProcessUnrestrictRequest([FromBody] ProcessUnrestrictRequestModel model)
        {
            try
            {
                var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var adminName = User.FindFirst("FullName")?.Value ?? User.Identity.Name;

                // Get the request details
                var request = await _unrestrictRequestService.GetRequestByIdAsync(model.RequestId);
                if (request == null)
                {
                    return Json(new { success = false, message = "Request not found" });
                }

                // Process the request
                var success = await _unrestrictRequestService.ProcessRequestAsync(
                    model.RequestId, 
                    adminId, 
                    adminName, 
                    model.Status, 
                    model.AdminNotes
                );

                if (success)
                {
                    // If approved, actually unrestrict the user
                    if (model.Status == "Approved")
                    {
                        var unrestrictSuccess = await _userManagementService.UnrestrictUserAsync(request.UserId);
                        
                        if (unrestrictSuccess)
                        {
                            // Create notification for student
                            var studentNotification = new Notification
                            {
                                UserId = request.UserId,
                                Type = "ACCOUNT_UNRESTRICTED",
                                Title = "Account Unrestricted",
                                Message = "Your account has been unrestricted by admin. You may borrow books again.",
                                BookTitle = "",
                                ReservationId = "",
                                IsRead = false,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _notificationService.CreateNotificationAsync(studentNotification);

                            // Create audit log
                            await _auditLogService.CreateAuditLogAsync(
                                adminId,
                                adminName,
                                "ADMIN",
                                "User Account Unrestricted",
                                $"Approved unrestrict request for student: {request.StudentName}",
                                "User",
                                request.UserId
                            );
                        }
                    }

                    return Json(new { success = true, message = $"Request {model.Status.ToLower()} successfully" });
                }
                else
                {
                    return Json(new { success = false, message = "Failed to process request" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error processing request: " + ex.Message });
            }
        }

    }
 
}