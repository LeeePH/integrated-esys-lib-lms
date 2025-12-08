using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;

namespace SystemLibrary.Services
{
    public class ReportService : IReportService
    {
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<ReturnTransaction> _returns;
        private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Penalty> _penalties;
        private readonly IMongoCollection<UnrestrictRequest> _unrestrictRequests;
        private readonly IMongoDatabase _database;

        public ReportService(IMongoDbService mongoDbService)
        {
            _books = mongoDbService.GetCollection<Book>("Books");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _returns = mongoDbService.GetCollection<ReturnTransaction>("Returns");
            _users = mongoDbService.GetCollection<User>("Users");
            _penalties = mongoDbService.GetCollection<Penalty>("Penalties");
            _unrestrictRequests = mongoDbService.GetCollection<UnrestrictRequest>("UnrestrictRequests");
            _database = mongoDbService.Database;
        }

        public async Task<BorrowingReportViewModel> GetBorrowingReportAsync(int daysRange = 30)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-daysRange);

            // Borrowed: reservations approved within date range
            var borrowedReservations = await _reservations
                .Find(r => r.ApprovalDate != null && r.ApprovalDate >= startDate && r.ApprovalDate < endDate)
                .ToListAsync();

            // Returned: all return transactions within date range (includes Good, Damaged-*, Lost)
            var returnedTx = await _returns
                .Find(r => r.CreatedAt >= startDate && r.CreatedAt < endDate)
                .ToListAsync();

            // Currently borrowed: books that are actually borrowed (not just approved)
            var currentlyBorrowed = await _reservations
                .CountDocumentsAsync(r => r.Status == "Borrowed");

            // Build monthly data within the range
            var monthlyData = new List<MonthlyBorrowingData>();
            var cursor = new DateTime(startDate.Year, startDate.Month, 1);
            var limit = new DateTime(endDate.Year, endDate.Month, 1).AddMonths(1);

            while (cursor < limit)
            {
                var monthStart = cursor;
                var monthEnd = cursor.AddMonths(1);

                var monthBorrowed = borrowedReservations.Count(r =>
                    r.ApprovalDate!.Value >= monthStart && r.ApprovalDate!.Value < monthEnd);

                var monthReturned = returnedTx.Count(rt =>
                    rt.CreatedAt >= monthStart && rt.CreatedAt < monthEnd);

                monthlyData.Add(new MonthlyBorrowingData
                {
                    Month = monthStart.ToString("MMM"),
                    Year = monthStart.Year,
                    TotalBorrowed = monthBorrowed,
                    TotalReturned = monthReturned
                });

                cursor = cursor.AddMonths(1);
            }

            // If we need full 12 months, fill in missing months
            if (daysRange >= 365)
            {
                monthlyData = FillMissingMonths(monthlyData, 12);
            }

            return new BorrowingReportViewModel
            {
                TotalBorrowing = borrowedReservations.Count,
                TotalReturns = returnedTx.Count,
                CurrentlyBorrowed = (int)currentlyBorrowed,
                MonthlyData = monthlyData,
                StartDate = startDate,
                EndDate = endDate,
                DaysRange = daysRange
            };
        }

        public async Task<object> GetDetailedBorrowingDataAsync(string type, int daysRange = 30)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-daysRange);

            switch (type.ToLower())
            {
                case "totalborrowing":
                    var borrowedReservations = await _reservations
                        .Find(r => r.ApprovalDate != null && r.ApprovalDate >= startDate && r.ApprovalDate < endDate)
                        .ToListAsync();

                    // Get book details for each reservation
                    var borrowedDetails = new List<object>();
                    foreach (var reservation in borrowedReservations)
                    {
                        Book? book = null;
                        if (ObjectId.TryParse(reservation.BookId, out ObjectId bookObjectId))
                        {
                            book = await _books.Find(b => b._id == bookObjectId).FirstOrDefaultAsync();
                        }
                        
                        borrowedDetails.Add(new
                        {
                            StudentName = reservation.FullName,
                            StudentNumber = reservation.StudentNumber,
                            BookTitle = book?.Title ?? reservation.BookTitle ?? "N/A",
                            BookAuthor = book?.Author ?? "N/A",
                            BorrowDate = reservation.ApprovalDate?.ToString("MMM dd, yyyy"),
                            DueDate = reservation.DueDate?.ToString("MMM dd, yyyy"),
                            Status = reservation.Status,
                            CopyIdentifier = reservation.CopyIdentifier
                        });
                    }

                    return new { type = "Total Borrowing", data = borrowedDetails };

                case "totalreturns":
                    var returnedTx = await _returns
                        .Find(r => r.CreatedAt >= startDate && r.CreatedAt < endDate)
                        .ToListAsync();

                    var returnedDetails = returnedTx.Select(r => new
                    {
                        StudentName = r.StudentName,
                        StudentNumber = r.StudentNumber,
                        BookTitle = r.BookTitle,
                        BookAuthor = "N/A", // Not available in ReturnTransaction
                        ReturnDate = r.CreatedAt.ToString("MMM dd, yyyy"),
                        Condition = r.BookCondition,
                        Remarks = r.Remarks,
                        CopyIdentifier = "N/A" // Not available in ReturnTransaction
                    }).ToList();

                    return new { type = "Total Returns", data = returnedDetails };

                case "currentlyborrowed":
                    var currentlyBorrowedReservations = await _reservations
                        .Find(r => r.Status == "Borrowed")
                        .ToListAsync();

                    // Get book details for each reservation
                    var currentlyBorrowedDetails = new List<object>();
                    foreach (var reservation in currentlyBorrowedReservations)
                    {
                        Book? book = null;
                        if (ObjectId.TryParse(reservation.BookId, out ObjectId bookObjectId))
                        {
                            book = await _books.Find(b => b._id == bookObjectId).FirstOrDefaultAsync();
                        }
                        
                        currentlyBorrowedDetails.Add(new
                        {
                            StudentName = reservation.FullName,
                            StudentNumber = reservation.StudentNumber,
                            BookTitle = book?.Title ?? reservation.BookTitle ?? "N/A",
                            BookAuthor = book?.Author ?? "N/A",
                            BorrowDate = reservation.ApprovalDate?.ToString("MMM dd, yyyy"),
                            DueDate = reservation.DueDate?.ToString("MMM dd, yyyy"),
                            DaysOverdue = reservation.DueDate.HasValue ? (DateTime.UtcNow - reservation.DueDate.Value).Days : 0,
                            CopyIdentifier = reservation.CopyIdentifier
                        });
                    }

                    return new { type = "Currently Borrowed", data = currentlyBorrowedDetails };

                default:
                    return new { type = "Unknown", data = new List<object>() };
            }
        }

        public async Task<object> GetDetailedInventoryDataAsync(string type, int daysRange = 30)
        {
            var today = DateTime.UtcNow;
            var startDate = today.AddDays(-daysRange);

            switch (type.ToLower())
            {
                case "totalbooks":
                    var allBooks = await _books.Find(_ => true).ToListAsync();
                    var totalBooksDetails = allBooks.Select(book => new
                    {
                        Title = book.Title,
                        Author = book.Author,
                        Subject = book.Subject,
                        ISBN = book.ISBN,
                        TotalCopies = book.TotalCopies,
                        AvailableCopies = book.AvailableCopies,
                        BorrowedCopies = book.TotalCopies - book.AvailableCopies,
                        Publisher = book.Publisher,
                        PublicationDate = book.PublicationDate.HasValue ? book.PublicationDate.Value.ToString("MMM dd, yyyy") : "N/A",
                        ClassificationNo = book.ClassificationNo
                    }).ToList();

                    return new { type = "Total Books", data = totalBooksDetails };

                case "totalavailable":
                    var availableBooks = await _books.Find(b => b.AvailableCopies > 0).ToListAsync();
                    var availableDetails = availableBooks.Select(book => new
                    {
                        Title = book.Title,
                        Author = book.Author,
                        Subject = book.Subject,
                        ISBN = book.ISBN,
                        AvailableCopies = book.AvailableCopies,
                        TotalCopies = book.TotalCopies,
                        Publisher = book.Publisher,
                        PublicationDate = book.PublicationDate.HasValue ? book.PublicationDate.Value.ToString("MMM dd, yyyy") : "N/A",
                        ClassificationNo = book.ClassificationNo
                    }).ToList();

                    return new { type = "Available Books", data = availableDetails };

                case "totalborrowed":
                    var borrowedBooks = await _books.Find(b => (b.TotalCopies - b.AvailableCopies) > 0).ToListAsync();
                    var borrowedDetails = borrowedBooks.Select(book => new
                    {
                        Title = book.Title,
                        Author = book.Author,
                        Subject = book.Subject,
                        ISBN = book.ISBN,
                        BorrowedCopies = book.TotalCopies - book.AvailableCopies,
                        TotalCopies = book.TotalCopies,
                        AvailableCopies = book.AvailableCopies,
                        Publisher = book.Publisher,
                        PublicationDate = book.PublicationDate.HasValue ? book.PublicationDate.Value.ToString("MMM dd, yyyy") : "N/A",
                        ClassificationNo = book.ClassificationNo
                    }).ToList();

                    return new { type = "Borrowed Books", data = borrowedDetails };

                case "totalreserved":
                    var reservations = await _reservations
                        .Find(r => r.Status == "Pending" && r.ReservationDate >= startDate && r.ReservationDate <= today)
                        .ToListAsync();

                    var reservedDetails = new List<object>();
                    foreach (var reservation in reservations)
                    {
                        Book? book = null;
                        if (ObjectId.TryParse(reservation.BookId, out ObjectId bookObjectId))
                        {
                            book = await _books.Find(b => b._id == bookObjectId).FirstOrDefaultAsync();
                        }

                        reservedDetails.Add(new
                        {
                            StudentName = reservation.FullName,
                            StudentNumber = reservation.StudentNumber,
                            BookTitle = book?.Title ?? reservation.BookTitle ?? "N/A",
                            BookAuthor = book?.Author ?? "N/A",
                            Subject = book?.Subject ?? "N/A",
                            ISBN = book?.ISBN ?? "N/A",
                            ReservationDate = reservation.ReservationDate.ToString("MMM dd, yyyy"),
                            Status = reservation.Status,
                            CopyIdentifier = reservation.CopyIdentifier
                        });
                    }

                    return new { type = "Reserved Books", data = reservedDetails };

                default:
                    return new { type = "Unknown", data = new List<object>() };
            }
        }

        private List<MonthlyBorrowingData> FillMissingMonths(List<MonthlyBorrowingData> data, int monthCount)
        {
            var result = new List<MonthlyBorrowingData>();
            var currentDate = DateTime.UtcNow.AddMonths(-monthCount + 1);

            for (int i = 0; i < monthCount; i++)
            {
                var month = currentDate.ToString("MMM");
                var year = currentDate.Year;

                var existing = data.FirstOrDefault(d => d.Month == month && d.Year == year);

                result.Add(existing ?? new MonthlyBorrowingData
                {
                    Month = month,
                    Year = year,
                    TotalBorrowed = 0,
                    TotalReturned = 0
                });

                currentDate = currentDate.AddMonths(1);
            }

            return result;
        }

        public async Task<OverdueReportViewModel> GetOverdueReportAsync(int daysRange = 30)
        {
            var today = DateTime.UtcNow;
            var startDate = today.AddDays(-daysRange);

            // Get all overdue reservations (only Borrowed status can be overdue since DueDate is set when book is picked up)
            var overdueReservations = await _reservations
                .Find(r => r.Status == "Borrowed" &&
                           r.DueDate != null &&
                           r.DueDate < today)
                .ToListAsync();

            // Get all overdue penalty records (unpaid or paid as needed) — include Overdue type
            var overduePenalties = await _penalties
                .Find(p => p.PenaltyType == "Overdue")
                .ToListAsync();

            // Get all restricted users (even if they don't have overdue books)
            var restrictedUsers = await _users
                .Find(u => u.Role == "student" && u.IsRestricted)
                .ToListAsync();

            // Group overdue reservations by user
            var overdueByUser = overdueReservations
                .GroupBy(r => r.UserId)
                .ToList();

            var overdueAccounts = new List<OverdueAccountDetail>();
            decimal totalFees = 0;
            int totalOverdueBooks = 0;

            foreach (var userGroup in overdueByUser)
            {
                var userId = userGroup.Key;
                var userObjectId = ObjectId.Parse(userId);

                // Get user info
                var user = await _users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                if (user == null) continue;

                // Get student profile for student number
                var studentProfile = await _database.GetCollection<StudentProfile>("StudentProfiles")
                    .Find(sp => sp.UserId == userObjectId)
                    .FirstOrDefaultAsync();

                var overdueBooks = new List<OverdueBookInfo>();
                decimal userTotalFees = 0;
                int maxDaysOverdue = 0;

                foreach (var reservation in userGroup)
                {
                    var bookObjectId = ObjectId.Parse(reservation.BookId);
                    var book = await _books.Find(b => b._id == bookObjectId).FirstOrDefaultAsync();

                    if (book != null && reservation.DueDate.HasValue)
                    {
                        var daysOverdue = (today - reservation.DueDate.Value).Days;
                        var lateFee = daysOverdue * 10m; // ₱10 per day

                        overdueBooks.Add(new OverdueBookInfo
                        {
                            BookTitle = book.Title,
                            DueDate = reservation.DueDate.Value,
                            DaysOverdue = daysOverdue,
                            LateFee = lateFee
                        });

                        userTotalFees += lateFee;
                        if (daysOverdue > maxDaysOverdue)
                            maxDaysOverdue = daysOverdue;
                    }
                }

                // Merge any penalty records for this user (e.g., created by background processor)
                var userPenaltyRecords = overduePenalties.Where(p => p.UserId.ToString() == userId).ToList();
                foreach (var pen in userPenaltyRecords)
                {
                    // Add penalty as an overdue book entry if not already present
                    overdueBooks.Add(new OverdueBookInfo
                    {
                        BookTitle = pen.BookTitle,
                        DueDate = pen.CreatedDate, // approximate
                        DaysOverdue = (today - pen.CreatedDate).Days,
                        LateFee = pen.Amount
                    });

                    userTotalFees += pen.Amount;
                    if ((today - pen.CreatedDate).Days > maxDaysOverdue)
                        maxDaysOverdue = (today - pen.CreatedDate).Days;
                }

                totalOverdueBooks += overdueBooks.Count;
                totalFees += userTotalFees;

                // Check if user is restricted (either in User.IsRestricted or StudentProfile.IsFlagged)
                bool isRestricted = user.IsRestricted || (studentProfile?.IsFlagged ?? false);
                
                // Check if user has a pending unrestrict request
                bool hasPendingRequest = await _unrestrictRequests
                    .Find(r => r.UserId == userId && r.Status == "Pending")
                    .AnyAsync();

                overdueAccounts.Add(new OverdueAccountDetail
                {
                    UserId = userId,
                    StudentName = user.FullName ?? "Unknown",
                    StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                    OverdueBookCount = overdueBooks.Count,
                    MaxDaysOverdue = maxDaysOverdue,
                    TotalLateFees = userTotalFees,
                    IsRestricted = isRestricted,
                    HasPendingRequest = hasPendingRequest,
                    OverdueBooks = overdueBooks
                });
            }

            // Add restricted users who don't have overdue books
            var processedUserIds = overdueAccounts.Select(a => a.UserId).ToHashSet();
            
            foreach (var restrictedUser in restrictedUsers)
            {
                var userId = restrictedUser._id.ToString();
                
                // Skip if already processed (has overdue books)
                if (processedUserIds.Contains(userId)) continue;
                
                // Get student profile for student number
                // Check if user has a pending unrestrict request
                bool hasPendingRequest = await _unrestrictRequests
                    .Find(r => r.UserId == userId && r.Status == "Pending")
                    .AnyAsync();

                var studentProfile = await _database.GetCollection<StudentProfile>("StudentProfiles")
                    .Find(sp => sp.UserId == restrictedUser._id)
                    .FirstOrDefaultAsync();
                
                // Check if user is restricted (either in User.IsRestricted or StudentProfile.IsFlagged)
                bool isRestricted = restrictedUser.IsRestricted || (studentProfile?.IsFlagged ?? false);
                
                // Only add if actually restricted
                if (isRestricted)
                {
                    overdueAccounts.Add(new OverdueAccountDetail
                    {
                        UserId = userId,
                        StudentName = restrictedUser.FullName ?? "Unknown",
                        StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                        OverdueBookCount = 0,
                        MaxDaysOverdue = 0,
                        TotalLateFees = 0,
                        IsRestricted = isRestricted,
                        HasPendingRequest = hasPendingRequest,
                        OverdueBooks = new List<OverdueBookInfo>()
                    });
                }
            }

            // Add users who have penalty records but weren't in overdueReservations or restrictedUsers
            var penaltyUserGroups = overduePenalties
                .GroupBy(p => p.UserId.ToString())
                .Where(g => !processedUserIds.Contains(g.Key))
                .ToList();

            foreach (var penGroup in penaltyUserGroups)
            {
                var userId = penGroup.Key;
                var userObjectId = ObjectId.Parse(userId);
                var user = await _users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                var studentProfile = await _database.GetCollection<StudentProfile>("StudentProfiles")
                    .Find(sp => sp.UserId == userObjectId)
                    .FirstOrDefaultAsync();

                decimal userTotalFees = 0;
                int maxDaysOverdue = 0;
                var overdueBooks = new List<OverdueBookInfo>();

                foreach (var pen in penGroup)
                {
                    overdueBooks.Add(new OverdueBookInfo
                    {
                        BookTitle = pen.BookTitle,
                        DueDate = pen.CreatedDate,
                        DaysOverdue = (today - pen.CreatedDate).Days,
                        LateFee = pen.Amount
                    });

                    userTotalFees += pen.Amount;
                    if ((today - pen.CreatedDate).Days > maxDaysOverdue)
                        maxDaysOverdue = (today - pen.CreatedDate).Days;
                }

                overdueAccounts.Add(new OverdueAccountDetail
                {
                    UserId = userId,
                    StudentName = user?.FullName ?? "Unknown",
                    StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                    OverdueBookCount = overdueBooks.Count,
                    MaxDaysOverdue = maxDaysOverdue,
                    TotalLateFees = userTotalFees,
                    IsRestricted = user?.IsRestricted ?? false,
                    HasPendingRequest = await _unrestrictRequests.Find(r => r.UserId == userId && r.Status == "Pending").AnyAsync(),
                    OverdueBooks = overdueBooks
                });
            }

            return new OverdueReportViewModel
            {
                TotalOverdueAccounts = overdueAccounts.Count,
                TotalOverdueBooks = totalOverdueBooks,
                TotalFees = totalFees,
                OverdueAccounts = overdueAccounts.OrderByDescending(a => a.MaxDaysOverdue).ThenBy(a => a.StudentName).ToList(),
                DaysRange = daysRange
            };
        }
        public async Task<ReportViewModel> GetCompleteReportAsync(string timeRange)
        {
            var viewModel = new ReportViewModel
            {
                TimeRange = timeRange,
                BorrowingTrend = await GetBorrowingTrendAsync(timeRange),
                OverdueAccounts = await GetOverdueAccountsAsync(),
                InventoryStatus = await GetInventoryStatusAsync(),
                StudentActivity = await GetStudentActivityAsync(timeRange)
            };

            return viewModel;
        }

        public async Task<BorrowingTrendReport> GetBorrowingTrendAsync(string timeRange)
        {
            var startDate = GetDateFilter(timeRange);

            var borrowedReservations = await _reservations
                .Find(r => r.Status == "Approved" && r.ApprovalDate != null && r.ApprovalDate >= startDate)
                .ToListAsync();

            // Get all return transactions (includes Good, Damaged-*, Lost conditions)
            var returnedBooks = await _returns
                .Find(r => r.CreatedAt >= startDate)
                .ToListAsync();

            var currentlyBorrowed = await _reservations
                .CountDocumentsAsync(r => r.Status == "Approved");

            var monthlyData = new List<MonthlyTrendData>(); // CHANGED
            var currentDate = startDate;

            while (currentDate <= DateTime.UtcNow)
            {
                var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var borrowed = borrowedReservations.Count(r =>
                    r.ApprovalDate.HasValue &&
                    r.ApprovalDate.Value >= monthStart &&
                    r.ApprovalDate.Value < monthEnd);

                // Count all returns including lost/damaged books
                var returned = returnedBooks.Count(r =>
                    r.CreatedAt >= monthStart &&
                    r.CreatedAt < monthEnd);

                monthlyData.Add(new MonthlyTrendData // CHANGED
                {
                    Month = monthStart.ToString("MMM yyyy"),
                    Borrowed = borrowed,
                    Returned = returned
                });

                currentDate = monthStart.AddMonths(1);
            }

            return new BorrowingTrendReport
            {
                TotalBorrowing = borrowedReservations.Count,
                TotalReturn = returnedBooks.Count,
                CurrentlyBorrowed = (int)currentlyBorrowed,
                MonthlyData = monthlyData
            };
        }

        public async Task<List<OverdueAccountReport>> GetOverdueAccountsAsync()
        {
            // Only "Borrowed" status can be overdue since DueDate is set when book is picked up
            var overdueReservations = await _reservations
                .Find(r => r.Status == "Borrowed" && r.DueDate != null && r.DueDate < DateTime.UtcNow)
                .ToListAsync();

            var overduePenalties = await _penalties
                .Find(p => p.PenaltyType == "Overdue")
                .ToListAsync();

            var overdueAccounts = new List<OverdueAccountReport>();

            foreach (var reservation in overdueReservations)
            {
                // Convert string IDs to ObjectId for comparison
                var userObjectId = ObjectId.Parse(reservation.UserId);
                var bookObjectId = ObjectId.Parse(reservation.BookId);

                var user = await _users.Find(u => u._id == userObjectId).FirstOrDefaultAsync();
                var book = await _books.Find(b => b._id == bookObjectId).FirstOrDefaultAsync();

                if (user != null && book != null && reservation.DueDate.HasValue)
                {
                    var daysOverdue = (DateTime.UtcNow - reservation.DueDate.Value).Days;
                    overdueAccounts.Add(new OverdueAccountReport
                    {
                        UserId = user._id.ToString(),
                        UserName = user.FullName ?? "Unknown",
                        BookTitle = book.Title ?? "Unknown",
                        DueDate = reservation.DueDate.Value,
                        DaysOverdue = daysOverdue,
                        LateFees = daysOverdue * 10
                    });
                }
            }

            // Include penalty records that may not have a current reservation (e.g., unpaid penalties)
            foreach (var pen in overduePenalties)
            {
                var userIdStr = pen.UserId.ToString();
                // If reservation-based entry for this user/book already exists, skip adding duplicate
                bool exists = overdueAccounts.Any(o => o.UserId == userIdStr && o.BookTitle == pen.BookTitle);
                if (exists) continue;

                overdueAccounts.Add(new OverdueAccountReport
                {
                    UserId = userIdStr,
                    UserName = pen.StudentName ?? "Unknown",
                    BookTitle = pen.BookTitle ?? "Unknown",
                    DueDate = pen.CreatedDate,
                    DaysOverdue = (DateTime.UtcNow - pen.CreatedDate).Days,
                    LateFees = pen.Amount
                });
            }

            return overdueAccounts;
        }

        public async Task<InventoryReportViewModel> GetInventoryReportAsync(int daysRange = 30)
        {
            var today = DateTime.UtcNow;
            var startDate = today.AddDays(-daysRange);

            // Fetch all books
            var allBooks = await _books.Find(_ => true).ToListAsync();

            // Fetch reservations within the date range
            var reservations = await _reservations
                .Find(r => r.ReservationDate >= startDate && r.ReservationDate <= today)
                .ToListAsync();

            // Group books by Subject (instead of Category)
            var categories = allBooks
                .GroupBy(b => b.Subject ?? "Uncategorized")
                .Select(group =>
                {
                    var categoryBooks = group.ToList();

                    int total = categoryBooks.Count(); // ✅ fixed Count()
                    int available = categoryBooks.Sum(b => b.AvailableCopies);
                    int totalCopies = categoryBooks.Sum(b => b.TotalCopies);
                    int borrowed = categoryBooks.Sum(b => b.BorrowedCopies);

                    // Compute reserved copies based on reservations
                    int reserved = reservations.Count(r =>
                        categoryBooks.Any(b => b._id.ToString() == r.BookId) &&
                        r.Status == "Pending");

                    double utilization = totalCopies > 0
                        ? ((double)(borrowed + reserved) / totalCopies) * 100
                        : 0;

                    return new CategoryInventory
                    {
                        Category = group.Key,
                        TotalBooks = totalCopies,
                        Borrowed = borrowed,
                        Reserved = reserved,
                        Available = available,
                        Utilization = Math.Round(utilization, 2)
                    };
                })
                .OrderByDescending(c => c.Utilization)
                .ToList();

            // Compute overall totals
            int totalBooks = categories.Sum(c => c.TotalBooks);
            int totalAvailable = categories.Sum(c => c.Available);
            int totalBorrowed = categories.Sum(c => c.Borrowed);
            int totalReserved = categories.Sum(c => c.Reserved);

            return new InventoryReportViewModel
            {
                Categories = categories,
                TotalBooks = totalBooks,
                TotalAvailable = totalAvailable,
                TotalBorrowed = totalBorrowed,
                TotalReserved = totalReserved,
                DaysRange = daysRange
            };
        }


        public async Task<InventoryStatusReport> GetInventoryStatusAsync()
        {
            var allBooks = await _books.Find(_ => true).ToListAsync();

            var allReservations = await _reservations
                .Find(r => r.Status == "Approved" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost")
                .ToListAsync();

            // Use string comparison for grouping
            var borrowCounts = allReservations
                .GroupBy(r => r.BookId)
                .ToDictionary(g => g.Key, g => g.Count());

            var lowStockBooks = allBooks
                .Where(b => b.AvailableCopies <= 2 && b.AvailableCopies > 0)
                .Select(b => new BookInventoryItem
                {
                    Title = b.Title ?? "Unknown",
                    Author = b.Author ?? "Unknown",
                    AvailableCopies = b.AvailableCopies
                })
                .ToList();

            var mostBorrowedBooks = allBooks
                .Select(b => new BookBorrowingStats
                {
                    Title = b.Title ?? "Unknown",
                    Author = b.Author ?? "Unknown",
                    BorrowCount = borrowCounts.ContainsKey(b._id.ToString()) ? borrowCounts[b._id.ToString()] : 0
                })
                .OrderByDescending(b => b.BorrowCount)
                .Take(5)
                .ToList();

            return new InventoryStatusReport
            {
                TotalBooks = allBooks.Count,
                TotalCopies = allBooks.Sum(b => b.TotalCopies),
                AvailableCopies = allBooks.Sum(b => b.AvailableCopies),
                BorrowedCopies = allBooks.Sum(b => b.BorrowedCopies),
                LowStockBooks = lowStockBooks,
                MostBorrowedBooks = mostBorrowedBooks
            };
        }

        public async Task<StudentActivityReportViewModel> GetStudentActivityReportAsync(int daysRange = 30)
        {
            var today = DateTime.UtcNow;
            var startDate = today.AddDays(-daysRange);

            // Get all reservations (we'll filter by BorrowDate and ReturnDate separately)
            var allReservations = await _reservations
                .Find(_ => true)
                .ToListAsync();

            // Get active borrowers (users who have borrowed in this period)
            var activeBorrowers = allReservations
                .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"))
                .Select(r => r.UserId)
                .Distinct()
                .Count();

            // Calculate average books per student
            var borrowingsByUser = allReservations
                .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"))
                .GroupBy(r => r.UserId)
                .ToList();

            decimal avgBooksPerStudent = borrowingsByUser.Any()
                ? (decimal)allReservations.Count(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost")) / borrowingsByUser.Count
                : 0;

            // Calculate average loan days
            var returnedBooks = allReservations
                .Where(r => (r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost") && r.ReturnDate.HasValue && r.ApprovalDate.HasValue)
                .ToList();

            decimal avgLoanDays = returnedBooks.Any()
                ? (decimal)returnedBooks.Average(r => (r.ReturnDate!.Value - r.ApprovalDate!.Value).TotalDays)
                : 0;

            // Get monthly activity data
            var monthlyActivity = new List<MonthlyActivityData>();
            var monthsToShow = daysRange > 90 ? 12 : Math.Min(12, (daysRange / 30) + 1);

            for (int i = monthsToShow - 1; i >= 0; i--)
            {
                var monthStart = today.AddMonths(-i).Date;
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Count borrowings by ApprovalDate within the month
                var monthBorrowings = allReservations
                    .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= monthStart && r.ApprovalDate.Value <= monthEnd &&
                               (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"))
                    .ToList();

                // Count returns by ReturnDate within the month
                var monthReturns = allReservations
                    .Where(r => (r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost") && r.ReturnDate.HasValue &&
                                r.ReturnDate >= monthStart && r.ReturnDate <= monthEnd)
                    .Count();

                var activeStudentsInMonth = monthBorrowings
                    .Select(r => r.UserId)
                    .Distinct()
                    .Count();

                monthlyActivity.Add(new MonthlyActivityData
                {
                    Month = monthStart.ToString("MMM"),
                    BooksBorrowed = monthBorrowings.Count,
                    BooksReturned = monthReturns,
                    ActiveStudents = activeStudentsInMonth
                });
            }

            // Find peak borrowing month
            var peakMonth = monthlyActivity.OrderByDescending(m => m.BooksBorrowed).FirstOrDefault();

            // Get most borrowed books
            var bookBorrowCounts = allReservations
                .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"))
                .GroupBy(r => r.BookId)
                .Select(g => new { BookId = g.Key, Count = g.Count(), UniqueUsers = g.Select(r => r.UserId).Distinct().Count() })
                .OrderByDescending(b => b.Count)
                .Take(10)
                .ToList();

            var mostBorrowedBooks = new List<MostBorrowedBookReport>();
            int maxBorrowCount = bookBorrowCounts.FirstOrDefault()?.Count ?? 1;

            foreach (var item in bookBorrowCounts)
            {
                var book = await _books.Find(b => b._id.ToString() == item.BookId).FirstOrDefaultAsync();
                if (book != null)
                {
                    mostBorrowedBooks.Add(new MostBorrowedBookReport
                    {
                        BookId = item.BookId,
                        Title = book.Title,
                        Author = book.Author,
                        BorrowCount = item.Count,
                        UniqueStudents = item.UniqueUsers,
                        Percentage = maxBorrowCount > 0 ? (decimal)item.Count / maxBorrowCount * 100 : 0
                    });
                }
            }

            // Get most active students
            var mostActiveStudents = new List<ActiveStudentInfo>();
            var topUsers = borrowingsByUser
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            foreach (var userGroup in topUsers)
            {
                var user = await _users.Find(u => u._id.ToString() == userGroup.Key).FirstOrDefaultAsync();
                if (user != null)
                {
                    var studentProfile = await _database.GetCollection<StudentProfile>("StudentProfiles")
                        .Find(sp => sp.UserId.ToString() == userGroup.Key)
                        .FirstOrDefaultAsync();

                    var userReservations = userGroup.ToList();
                    var currentBorrowings = userReservations.Count(r => r.Status == "Approved");
                    var lastBorrow = userReservations.OrderByDescending(r => r.ReservationDate).FirstOrDefault();

                    var userReturnedBooks = userReservations
                        .Where(r => (r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost") && r.ReturnDate.HasValue && r.ApprovalDate.HasValue)
                        .ToList();

                    decimal userAvgDays = userReturnedBooks.Any()
                        ? (decimal)userReturnedBooks.Average(r => (r.ReturnDate!.Value - r.ApprovalDate!.Value).TotalDays)
                        : 0;

                    mostActiveStudents.Add(new ActiveStudentInfo
                    {
                        UserId = userGroup.Key,
                        StudentName = user.FullName ?? user.Username ?? "Unknown",
                        StudentNumber = studentProfile?.StudentNumber ?? "N/A",
                        TotalBorrowings = userReservations.Count,
                        CurrentBorrowings = currentBorrowings,
                        LastBorrowDate = lastBorrow?.ReservationDate ?? DateTime.MinValue,
                        AverageLoanDays = userAvgDays
                    });
                }
            }

            // ------------------ ADD COURSE SUMMARY ------------------
            var studentProfilesAll = await _database.GetCollection<StudentProfile>("StudentProfiles").Find(_ => true).ToListAsync();

            var courseSummary = allReservations
                .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"))
                .GroupBy(r =>
                {
                    var profile = studentProfilesAll.FirstOrDefault(sp => sp.UserId.ToString() == r.UserId);
                    return profile?.Course ?? "Unknown";
                })
                .Select((g, index) => new CourseSummaryViewModel
                {
                    Course = g.Key,
                    TotalBorrowings = g.Count(),
                    TotalReturned = g.Count(r => r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost"),
                    TotalOverdue = g.Count(r => r.Status == "Overdue"),
                    Rank = index + 1
                })
                .OrderByDescending(c => c.TotalBorrowings)
                .ToList();

            // ------------------ RETURN VIEWMODEL ------------------
            return new StudentActivityReportViewModel
            {
                ActiveBorrowers = activeBorrowers,
                AverageBooksPerStudent = Math.Round(avgBooksPerStudent, 1),
                AverageLoanDays = Math.Round(avgLoanDays, 1),
                TotalBorrowings = allReservations.Count(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                           (r.Status == "Borrowed" || r.Status == "Returned" || r.Status == "Damaged" || r.Status == "Lost")),
                DaysRange = daysRange,
                MonthlyActivity = monthlyActivity,
                MostBorrowedBooks = mostBorrowedBooks,
                MostActiveStudents = mostActiveStudents,
                PeakBorrowingMonth = peakMonth?.Month ?? "",
                PeakBorrowingCount = peakMonth?.BooksBorrowed ?? 0,
                CourseSummary = courseSummary // <--- now populated
            };
        }


        public async Task<List<StudentActivityReport>> GetStudentActivityAsync(string timeRange)
        {
            var startDate = GetDateFilter(timeRange);
            var students = await _users.Find(u => u.Role == "student").ToListAsync();
            var studentActivity = new List<StudentActivityReport>();
            
            Console.WriteLine($"[ReportService] GetStudentActivityAsync - TimeRange: {timeRange}, StartDate: {startDate:yyyy-MM-dd HH:mm:ss}");

            foreach (var student in students)
            {
                var studentIdStr = student._id.ToString();

                var allReservations = await _reservations
                    .Find(r => r.UserId == studentIdStr)
                    .ToListAsync();

                // Count borrowings that were actually borrowed within the time range
                // Only count books that have been marked as borrowed (status is not "Approved" or "Pending")
                var borrowingsInRange = allReservations
                    .Where(r => r.ApprovalDate.HasValue && r.ApprovalDate.Value >= startDate && 
                               r.Status != "Approved" && r.Status != "Pending" && r.Status != "Rejected")
                    .ToList();

                var currentBorrowings = allReservations.Count(r => r.Status == "Approved");
                var overdueBooks = allReservations.Count(r =>
                    r.Status == "Approved" &&
                    r.DueDate.HasValue &&
                    r.DueDate.Value < DateTime.UtcNow);

                // Returns within the selected time range - use ObjectId to match ReturnTransaction.UserId
                var userObjectId = student._id;
                var returnsInRange = await _returns
                    .Find(r => r.UserId == userObjectId && r.CreatedAt >= startDate)
                    .ToListAsync();

                // Also get ALL returns for this user to debug
                var allReturnsForUser = await _returns
                    .Find(r => r.UserId == userObjectId)
                    .ToListAsync();

                // Include all penalty types: late, damage, lost — filtered by time range
                var totalLateFees = returnsInRange.Sum(r => r.TotalPenalty);

                // Debug logging
                if (borrowingsInRange.Any() || returnsInRange.Any() || allReturnsForUser.Any())
                {
                    Console.WriteLine($"[ReportService] Student {student.FullName}:");
                    Console.WriteLine($"[ReportService] - Time range: {startDate:yyyy-MM-dd HH:mm:ss} to {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"[ReportService] - Borrowings approved in range: {borrowingsInRange.Count}");
                    Console.WriteLine($"[ReportService] - Returns made in range: {returnsInRange.Count}");
                    Console.WriteLine($"[ReportService] - Total activity (borrowings + returns): {borrowingsInRange.Count + returnsInRange.Count}");
                    Console.WriteLine($"[ReportService] - Current borrowings: {currentBorrowings}");
                    Console.WriteLine($"[ReportService] - Total penalties: {totalLateFees}");
                    
                    // Debug: Show all reservations and their statuses
                    Console.WriteLine($"[ReportService] - All reservations for this student:");
                    foreach (var res in allReservations)
                    {
                        Console.WriteLine($"[ReportService]   Reservation: {res.BookTitle}, Status: {res.Status}, ApprovalDate: {res.ApprovalDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"}, ReservationDate: {res.ReservationDate:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    // Debug: Show ALL returns for this user
                    Console.WriteLine($"[ReportService] - ALL returns for this student:");
                    foreach (var ret in allReturnsForUser)
                    {
                        var inRange = ret.CreatedAt >= startDate ? "YES" : "NO";
                        Console.WriteLine($"[ReportService]   Return: {ret.BookTitle}, Condition: {ret.BookCondition}, CreatedAt: {ret.CreatedAt:yyyy-MM-dd HH:mm:ss}, InRange: {inRange}, TotalPenalty: {ret.TotalPenalty}");
                    }
                }

                // Latest activity from reservations or returns
                var latestReservationActivity = allReservations
                    .OrderByDescending(r => r.ReservationDate)
                    .FirstOrDefault()?.ReservationDate;

                var latestReturnActivity = returnsInRange
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault()?.CreatedAt;

                var lastActivity = new[] { latestReservationActivity, latestReturnActivity }
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .DefaultIfEmpty(student.CreatedAt)
                    .Max();

                // Include student if they had borrowings in range, current borrowings, or returns in range
                if (borrowingsInRange.Any() || currentBorrowings > 0 || returnsInRange.Any())
                {
                    studentActivity.Add(new StudentActivityReport
                    {
                        UserId = student._id.ToString(),
                        UserName = student.FullName ?? "Unknown",
                        Email = student.Email ?? "N/A",
                        // Count total activity: borrowings approved in time range + returns made in time range
                        // This represents total library activity (borrowing + returning) in the selected period
                        // All return types (Good, Damaged-*, Lost) should be counted equally
                        TotalBorrowings = borrowingsInRange.Count + returnsInRange.Count,
                        CurrentBorrowings = currentBorrowings,
                        OverdueBooks = overdueBooks,
                        TotalLateFees = totalLateFees,
                        LastActivity = lastActivity
                    });
                }
            }

            return studentActivity.OrderByDescending(s => s.TotalBorrowings).ToList();
        }

        private DateTime GetDateFilter(string timeRange)
        {
            return timeRange switch
            {
                "Last30Days" => DateTime.UtcNow.AddDays(-30),
                "Last90Days" => DateTime.UtcNow.AddDays(-90),
                "LastYear" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.UtcNow.AddDays(-30)
            };
        }
    }


}

