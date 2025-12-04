using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IAuthorService
    {
        Task<List<Author>> GetAllAsync();
        Task<Author?> GetByIdAsync(string id);
        Task<Author?> GetByNameAsync(string name);
        Task<bool> CreateAsync(Author author);
        Task<bool> UpdateAsync(string id, Author author);
        Task<bool> DeleteAsync(string id);

        // Lookup books by author name from existing Books collection
        Task<List<Book>> GetBooksByAuthorAsync(string authorName);
    }
}


