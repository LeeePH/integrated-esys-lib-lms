using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IBookCopyService
    {
        // Basic CRUD operations
        Task<BookCopy?> GetBookCopyByIdAsync(string copyId);
        Task<BookCopy?> GetBookCopyByBarcodeAsync(string barcode);
        Task<List<BookCopy>> GetBookCopiesByBookIdAsync(string bookId);
        Task<List<BookCopy>> GetAvailableCopiesByBookIdAsync(string bookId);
        Task<BookCopy> CreateBookCopyAsync(BookCopy bookCopy);
        Task<bool> UpdateBookCopyAsync(BookCopy bookCopy);
        Task<bool> DeleteBookCopyAsync(string copyId);

        // Copy management operations
        Task<List<BookCopy>> CreateMultipleCopiesAsync(string bookId, int numberOfCopies, string createdBy);
        Task<bool> UpdateCopyStatusAsync(string copyId, BookCopyStatus status);
        Task<bool> UpdateCopyConditionAsync(string copyId, CopyCondition condition, string notes = "");
        Task<bool> MarkCopyAsLostAsync(string copyId, string reason = "");
        Task<bool> MarkCopyAsDamagedAsync(string copyId, string damageNotes = "");
        Task<bool> MarkCopyAsFoundAsync(string copyId);
        Task<bool> MarkCopyAsRepairedAsync(string copyId);

        // Availability and status checks
        Task<bool> IsCopyAvailableAsync(string copyId);
        Task<bool> IsCopyBorrowedAsync(string copyId);
        Task<BookCopyStatus> GetCopyStatusAsync(string copyId);
        Task<List<BookCopy>> GetCopiesByStatusAsync(BookCopyStatus status);
        Task<List<BookCopy>> GetLostCopiesAsync();
        Task<List<BookCopy>> GetDamagedCopiesAsync();

        // Statistics and reporting
        Task<int> GetTotalCopiesCountAsync(string bookId);
        Task<int> GetAvailableCopiesCountAsync(string bookId);
        Task<int> GetBorrowedCopiesCountAsync(string bookId);
        Task<int> GetLostCopiesCountAsync(string bookId);
        Task<int> GetDamagedCopiesCountAsync(string bookId);
        Task<Dictionary<BookCopyStatus, int>> GetCopyStatusSummaryAsync(string bookId);

        // Copy generation and management
        Task<string> GenerateNextCopyIdAsync(string bookId);
        Task<bool> ValidateCopyIdAsync(string copyId);
        Task<List<BookCopy>> GetCopiesNeedingAttentionAsync(); // Lost, damaged, overdue
    }
}
