using SystemLibrary.ViewModels;

namespace SystemLibrary.Services
{
    public interface ITransactionService
    {
        Task<StudentInfoViewModel> GetStudentInfoAsync(string studentId);
        Task<BookInfoViewModel> GetBookInfoAsync(string bookId);
        Task<BorrowingEligibilityViewModel> CheckBorrowingEligibilityAsync(string studentId, string bookId);
        Task<bool> ProcessDirectBorrowingAsync(DirectBorrowingRequest request);
    }
}