using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;

namespace SystemLibrary.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;
        private readonly IMongoCollection<Book> _books;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IPenaltyService _penaltyService;
        private readonly INotificationService _notificationService;

        public TransactionService(IMongoDbService mongoDbService, IPenaltyService penaltyService, INotificationService notificationService)
        {
            _users = mongoDbService.GetCollection<User>("Users");
            _studentProfiles = mongoDbService.GetCollection<StudentProfile>("StudentProfiles");
            _books = mongoDbService.GetCollection<Book>("Books");
            _reservations = mongoDbService.GetCollection<Reservation>("Reservations");
            _penaltyService = penaltyService;
            _notificationService = notificationService;
        }

        public async Task<StudentInfoViewModel> GetStudentInfoAsync(string studentId)
        {
            try
            {
                // Try to find by ObjectId first
                User user = null;
                StudentProfile profile = null;
                if (ObjectId.TryParse(studentId, out ObjectId objectId))
                {
                    user = await _users.Find(u => u._id == objectId).FirstOrDefaultAsync();
                    if (user != null)
                    {
                        profile = await _studentProfiles.Find(sp => sp.UserId == objectId).FirstOrDefaultAsync();
                    }
                }
                // If not found, try by student number
                if (user == null)
                {
                    profile = await _studentProfiles.Find(sp => sp.StudentNumber == studentId).FirstOrDefaultAsync();
                    if (profile != null)
                    {
                        user = await _users.Find(u => u._id == profile.UserId).FirstOrDefaultAsync();
                    }
                }
                if (user == null)
                {
                    return null;
                }
                // Get borrowed books count
                var borrowedCount = await _reservations.CountDocumentsAsync(r =>
                    r.UserId == user._id.ToString() &&
                    r.Status == "Approved");
                // Get overdue books count
                var overdueCount = await _reservations.CountDocumentsAsync(r =>
                    r.UserId == user._id.ToString() &&
                    r.Status == "Approved" &&
                    r.DueDate.HasValue &&
                    r.DueDate.Value < DateTime.UtcNow);
                // Determine student status
                string status = "good";
                if (!user.IsActive)
                {
                    status = "restricted";
                }
                else if (user.HasPendingPenalties)
                {
                    status = "penalty";
                }
                else if (borrowedCount >= 3) // Assuming max 3 books
                {
                    status = "limit";
                }
                else if (overdueCount > 0)
                {
                    status = "restricted";
                }

                return new StudentInfoViewModel
                {
                    StudentId = user._id.ToString(),
                    Name = user.FullName,
                    Email = user.Email,
                    IdNumber = profile?.StudentNumber ?? "N/A",
                    Status = status,
                    EnrollmentStatus = user.IsActive ? "Active" : "Inactive",
                    BorrowedBooks = (int)borrowedCount,
                    MaxBooksAllowed = 3,
                    OverdueBooks = (int)overdueCount,
                    HasPendingPenalties = user.HasPendingPenalties,
                    TotalPendingPenalties = user.TotalPendingPenalties
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetStudentInfoAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<BookInfoViewModel> GetBookInfoAsync(string bookId)
        {
            try
            {
                Book book = null;
                // Try to find by ObjectId
                if (ObjectId.TryParse(bookId, out ObjectId objectId))
                {
                    book = await _books.Find(b => b._id == objectId).FirstOrDefaultAsync();
                }
                // If not found, try by ISBN or title
                if (book == null)
                {
                    var filter = Builders<Book>.Filter.Or(
                        Builders<Book>.Filter.Eq(b => b.ISBN, bookId),
                        Builders<Book>.Filter.Regex(b => b.Title, new BsonRegularExpression(bookId, "i"))
                    );
                    book = await _books.Find(filter).FirstOrDefaultAsync();
                }
                if (book == null)
                {
                    return null;
                }
                return new BookInfoViewModel
                {
                    BookId = book._id.ToString(),
                    Title = book.Title,
                    Author = book.Author,
                    AccessionNumber = book._id.ToString().Substring(0, 8),
                    Status = book.AvailableCopies > 0 ? "available" : "unavailable",
                    ISBN = book.ISBN,
                    Location = book.Subject ?? "General Collection",
                    AvailableCopies = book.AvailableCopies,
                    TotalCopies = book.TotalCopies,
                    IsActive = book.IsActive,
                    IsReferenceOnly = book.IsReferenceOnly
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetBookInfoAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<BorrowingEligibilityViewModel> CheckBorrowingEligibilityAsync(string studentId, string bookId)
        {
            try
            {
                var student = await GetStudentInfoAsync(studentId);
                var book = await GetBookInfoAsync(bookId);

                if (student == null || book == null)
                {
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = false,
                        Message = "Student or Book not found",
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                // Check eligibility
                bool isEligible = true;
                string message = "Eligible to borrow";

                if (student.Status == "restricted")
                {
                    isEligible = false;
                    message = "Student account is restricted";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (student.HasPendingPenalties)
                {
                    isEligible = false;
                    message = $"Student has pending penalties of ₱{student.TotalPendingPenalties:F2}. Please settle penalties before borrowing.";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                // Double-check with penalty service
                var penalties = await _penaltyService.GetUserPenaltiesAsync(studentId);
                var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();

                if (pendingPenalties.Any())
                {
                    var totalPenalties = pendingPenalties.Sum(p => p.Amount);
                    isEligible = false;
                    message = $"Student has {pendingPenalties.Count} pending penalties totaling ₱{totalPenalties:F2}. Please settle penalties before borrowing.";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (student.BorrowedBooks >= student.MaxBooksAllowed)
                {
                    isEligible = false;
                    message = "Student has reached borrowing limit";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (student.OverdueBooks > 0)
                {
                    isEligible = false;
                    message = "Student has overdue books";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (!book.IsActive)
                {
                    isEligible = false;
                    message = "Book is inactive";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (book.IsReferenceOnly)
                {
                    isEligible = false;
                    message = "Book is for reference only";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                if (book.AvailableCopies <= 0)
                {
                    isEligible = false;
                    message = "Book is not available";
                    return new BorrowingEligibilityViewModel
                    {
                        IsEligible = isEligible,
                        Message = message,
                        BorrowDate = DateTime.UtcNow,
                        DueDate = DateTime.UtcNow.AddDays(14),
                        LoanPeriodDays = 14
                    };
                }

                // All checks passed - eligible
                return new BorrowingEligibilityViewModel
                {
                    IsEligible = true,
                    Message = "Eligible to borrow",
                    BorrowDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14),
                    LoanPeriodDays = 14
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckBorrowingEligibilityAsync: {ex.Message}");
                return new BorrowingEligibilityViewModel
                {
                    IsEligible = false,
                    Message = "Error checking eligibility",
                    BorrowDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(14),
                    LoanPeriodDays = 14
                };
            }
        }

        public async Task<bool> ProcessDirectBorrowingAsync(DirectBorrowingRequest request)
        {
            try
            {
                // Verify student exists
                var studentInfo = await GetStudentInfoAsync(request.StudentId);
                if (studentInfo == null)
                {
                    return false;
                }

                // Verify book exists and is available and borrowable
                var bookInfo = await GetBookInfoAsync(request.BookId);
                if (bookInfo == null || !bookInfo.IsActive || bookInfo.IsReferenceOnly || bookInfo.AvailableCopies <= 0)
                {
                    return false;
                }

                // Check eligibility
                var eligibility = await CheckBorrowingEligibilityAsync(request.StudentId, request.BookId);
                if (!eligibility.IsEligible)
                {
                    return false;
                }

                // Get book details for reservation
                var book = await _books.Find(b => b._id.ToString() == request.BookId ||
                                                   b.ISBN == request.BookId)
                    .FirstOrDefaultAsync();
                if (book == null)
                {
                    return false;
                }

                // Create reservation directly as "Borrowed" (fully automatic)
                var reservation = new Reservation
                {
                    _id = ObjectId.GenerateNewId().ToString(),
                    UserId = studentInfo.StudentId,
                    BookId = book._id.ToString(),
                    BookTitle = book.Title,
                    Status = "Borrowed", // Directly borrowed - no approval needed
                    ReservationDate = request.BorrowDate,
                    ApprovalDate = request.BorrowDate,
                    DueDate = request.DueDate,
                    ApprovedBy = request.LibrarianId,
                    ReservationType = "Direct Borrowing",
                    StudentNumber = studentInfo.IdNumber,
                    FullName = studentInfo.Name,
                    BorrowType = "WALK IN" // Librarian transactions are WALK IN
                };

                // Insert reservation
                await _reservations.InsertOneAsync(reservation);

                // Update book availability
                var updateBook = Builders<Book>.Update.Inc(b => b.AvailableCopies, -1);
                await _books.UpdateOneAsync(b => b._id == book._id, updateBook);

                // Send notification to student about the borrowed book
                await _notificationService.CreateBookBorrowedNotificationAsync(
                    studentInfo.StudentId,
                    book.Title,
                    request.DueDate
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessDirectBorrowingAsync: {ex.Message}");
                return false;
            }
        }
    }
}