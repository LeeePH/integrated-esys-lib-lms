using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IBookService
    {
        // Existing methods
        Task<List<Book>> GetAllBooksAsync();
        Task<Book> GetBookByIdAsync(string bookId);
        Task<List<Book>> GetAvailableBooksAsync();
        Task<bool> BorrowBookAsync(string bookId);
        Task<bool> ReturnBookAsync(string bookId);

        // Add these missing CRUD methods
        Task<bool> AddBookAsync(Book book);
        Task<bool> UpdateBookAsync(string bookId, Book book);
        Task<bool> DeleteBookAsync(string bookId);
        Task<List<Book>> SearchBooksAsync(string searchTerm);
        
        // Dynamic recommendation methods
        Task<Book?> GetBookOfTheDayAsync();
        Task<List<Book>> GetTrendingBooksAsync(int count = 10);
        Task<List<Book>> GetRecommendedBooksAsync(string? userId = null, int count = 10);
    }
}