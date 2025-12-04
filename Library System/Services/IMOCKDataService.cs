using MongoDB.Driver;
using MongoDB.Bson;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public interface IMOCKDataService
    {
        // Student MOCK data operations
        Task<StudentMOCKData?> GetStudentByNumberAsync(string studentNumber);
        Task<List<StudentMOCKData>> GetAllStudentsAsync();
        Task<bool> ValidateStudentEnrollmentAsync(string studentNumber);
        Task<bool> AddStudentMOCKDataAsync(StudentMOCKData studentData);
        Task<bool> UpdateStudentMOCKDataAsync(string studentNumber, StudentMOCKData studentData);

        // Staff MOCK data operations
        Task<StaffMOCKData?> GetStaffByEmployeeIdAsync(string employeeId);
        Task<List<StaffMOCKData>> GetAllStaffAsync();
        Task<bool> ValidateStaffEmploymentAsync(string employeeId);
        Task<bool> AddStaffMOCKDataAsync(StaffMOCKData staffData);
        Task<bool> UpdateStaffMOCKDataAsync(string employeeId, StaffMOCKData staffData);

        // Bulk operations
        Task<bool> BulkInsertStudentsAsync(List<StudentMOCKData> students);
        Task<bool> BulkInsertStaffAsync(List<StaffMOCKData> staff);
        Task<bool> ClearAllMACDataAsync();
        
        // Book MOCK data operations
        Task<BookMOCKData?> GetBookByIsbnAsync(string isbn);
        Task<List<BookMOCKData>> GetBooksByAuthorAsync(string authorName);

        // Database access
        IMongoDatabase GetDatabase();
    }
}
