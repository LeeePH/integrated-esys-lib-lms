using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface ISubjectService
    {
        Task<List<Subject>> GetAllAsync();
        Task<Subject?> GetByIdAsync(string id);
        Task<Subject?> GetByNameAsync(string name);
        Task<bool> CreateAsync(Subject subject);
        Task<bool> UpdateAsync(string id, Subject subject);
        Task<bool> DeleteAsync(string id);

        Task<List<Book>> GetBooksBySubjectAsync(string subjectName);
    }
}


