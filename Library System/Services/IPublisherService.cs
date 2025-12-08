using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IPublisherService
    {
        Task<List<Publisher>> GetAllAsync();
        Task<Publisher?> GetByIdAsync(string id);
        Task<Publisher?> GetByNameAsync(string name);
        Task<bool> CreateAsync(Publisher publisher);
        Task<bool> UpdateAsync(string id, Publisher publisher);
        Task<bool> DeleteAsync(string id);

        Task<List<Book>> GetBooksByPublisherAsync(string publisherName);
    }
}


