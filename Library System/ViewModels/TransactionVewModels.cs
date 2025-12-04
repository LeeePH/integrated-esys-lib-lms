using MongoDB.Bson;

namespace SystemLibrary.ViewModels
{
    public class StudentInfoViewModel
    {
        public string StudentId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string IdNumber { get; set; }
        public string Status { get; set; }
        public string EnrollmentStatus { get; set; }
        public int BorrowedBooks { get; set; }
        public int MaxBooksAllowed { get; set; }
        public int OverdueBooks { get; set; }
        public bool HasPendingPenalties { get; set; }
        public decimal TotalPendingPenalties { get; set; }
    }

    public class BookInfoViewModel
    {
        public string BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string AccessionNumber { get; set; }
        public string Status { get; set; }
        public string ISBN { get; set; }
        public string Location { get; set; }
        public int AvailableCopies { get; set; }
        public int TotalCopies { get; set; }
        public bool IsActive { get; set; }
        public bool IsReferenceOnly { get; set; }
    }

    public class BorrowingEligibilityViewModel
    {
        public bool IsEligible { get; set; }
        public string Message { get; set; }
        public DateTime? BorrowDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int LoanPeriodDays { get; set; }
    }

    public class DirectBorrowingRequest
    {
        public string StudentId { get; set; }
        public string BookId { get; set; }
        public string LibrarianId { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public int LoanPeriodDays { get; set; }
    }
}