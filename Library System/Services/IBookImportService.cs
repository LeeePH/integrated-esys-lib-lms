using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IBookImportService
    {
        // Stage an import record
        Task<bool> StageAsync(string isbn, int quantity, string? notes = null);

        // Process a staged record: fetch MOCK data, create/merge Book, adjust copies
        Task<bool> ProcessAsync(string importId);

        // Convenience: process by ISBN directly
        Task<bool> ProcessByIsbnAsync(string isbn, int quantity);

        // Bulk: add all books by an author from MOCK data
        Task<int> ProcessByAuthorAsync(string authorName, int quantityPerTitle);

        // Get staged items
        Task<List<DummyBookImport>> GetStagedAsync(string? status = null);
    }
}


