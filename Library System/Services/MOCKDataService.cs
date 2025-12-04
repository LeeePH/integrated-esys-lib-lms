using MongoDB.Driver;
using MongoDB.Bson;
using SystemLibrary.Models;

namespace SystemLibrary.Services
{
    public class MOCKDataService : IMOCKDataService
    {
        private readonly IMongoCollection<StudentMOCKData> _StudentMOCKData;
        private readonly IMongoCollection<StaffMOCKData> _StaffMOCKData;
        private readonly IMongoCollection<BookMOCKData> _BookMOCKData;
        private readonly IMongoDatabase _database;

        public MOCKDataService(IMongoDatabase database)
        {
            _database = database;
            _StudentMOCKData = database.GetCollection<StudentMOCKData>("StudentMOCKData");
            _StaffMOCKData = database.GetCollection<StaffMOCKData>("StaffMOCKData");
            _BookMOCKData = database.GetCollection<BookMOCKData>("BookMOCKData");
        }

        // Student MOCK data operations
        public async Task<StudentMOCKData?> GetStudentByNumberAsync(string studentNumber)
        {
            try
            {
                var filter = Builders<StudentMOCKData>.Filter.Eq(s => s.StudentNumber, studentNumber);
                return await _StudentMOCKData.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting student by number: {ex.Message}");
                return null;
            }
        }

        public async Task<List<StudentMOCKData>> GetAllStudentsAsync()
        {
            try
            {
                return await _StudentMOCKData.Find(_ => true).ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all students: {ex.Message}");
                return new List<StudentMOCKData>();
            }
        }

        public async Task<bool> ValidateStudentEnrollmentAsync(string studentNumber)
        {
            try
            {
                var student = await GetStudentByNumberAsync(studentNumber);
                return student != null && student.IsEnrolled && student.IsActive;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating student enrollment: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddStudentMOCKDataAsync(StudentMOCKData studentData)
        {
            try
            {
                // Check if student already exists
                var existingStudent = await GetStudentByNumberAsync(studentData.StudentNumber);
                if (existingStudent != null)
                {
                    return false; // Student already exists
                }

                studentData.CreatedAt = DateTime.UtcNow;
                studentData.UpdatedAt = DateTime.UtcNow;
                await _StudentMOCKData.InsertOneAsync(studentData);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding student MOCK data: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateStudentMOCKDataAsync(string studentNumber, StudentMOCKData studentData)
        {
            try
            {
                var filter = Builders<StudentMOCKData>.Filter.Eq(s => s.StudentNumber, studentNumber);
                studentData.UpdatedAt = DateTime.UtcNow;
                
                var result = await _StudentMOCKData.ReplaceOneAsync(filter, studentData);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating student MOCK data: {ex.Message}");
                return false;
            }
        }

        // Staff MOCK data operations
        public async Task<StaffMOCKData?> GetStaffByEmployeeIdAsync(string employeeId)
        {
            try
            {
                var filter = Builders<StaffMOCKData>.Filter.Eq(s => s.EmployeeId, employeeId);
                return await _StaffMOCKData.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff by employee ID: {ex.Message}");
                return null;
            }
        }

        public async Task<List<StaffMOCKData>> GetAllStaffAsync()
        {
            try
            {
                return await _StaffMOCKData.Find(_ => true).ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting all staff: {ex.Message}");
                return new List<StaffMOCKData>();
            }
        }

        public async Task<bool> ValidateStaffEmploymentAsync(string employeeId)
        {
            try
            {
                var staff = await GetStaffByEmployeeIdAsync(employeeId);
                return staff != null && staff.IsActive;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating staff employment: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddStaffMOCKDataAsync(StaffMOCKData staffData)
        {
            try
            {
                // Check if staff already exists
                var existingStaff = await GetStaffByEmployeeIdAsync(staffData.EmployeeId);
                if (existingStaff != null)
                {
                    return false; // Staff already exists
                }

                staffData.CreatedAt = DateTime.UtcNow;
                staffData.UpdatedAt = DateTime.UtcNow;
                await _StaffMOCKData.InsertOneAsync(staffData);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding staff MOCK data: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateStaffMOCKDataAsync(string employeeId, StaffMOCKData staffData)
        {
            try
            {
                var filter = Builders<StaffMOCKData>.Filter.Eq(s => s.EmployeeId, employeeId);
                staffData.UpdatedAt = DateTime.UtcNow;
                
                var result = await _StaffMOCKData.ReplaceOneAsync(filter, staffData);
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating staff MOCK data: {ex.Message}");
                return false;
            }
        }

        // Bulk operations
        public async Task<bool> BulkInsertStudentsAsync(List<StudentMOCKData> students)
        {
            try
            {
                if (students == null || !students.Any())
                    return false;

                // Set timestamps
                foreach (var student in students)
                {
                    student.CreatedAt = DateTime.UtcNow;
                    student.UpdatedAt = DateTime.UtcNow;
                }

                await _StudentMOCKData.InsertManyAsync(students);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error bulk inserting students: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> BulkInsertStaffAsync(List<StaffMOCKData> staff)
        {
            try
            {
                if (staff == null || !staff.Any())
                    return false;

                // Set timestamps
                foreach (var staffMember in staff)
                {
                    staffMember.CreatedAt = DateTime.UtcNow;
                    staffMember.UpdatedAt = DateTime.UtcNow;
                }

                await _StaffMOCKData.InsertManyAsync(staff);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error bulk inserting staff: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearAllMACDataAsync()
        {
            try
            {
                await _StudentMOCKData.DeleteManyAsync(_ => true);
                await _StaffMOCKData.DeleteManyAsync(_ => true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing MOCK data: {ex.Message}");
                return false;
            }
        }

        // Book lookup by ISBN (forgiving: ignores hyphens/spaces, case-insensitive)
        public async Task<BookMOCKData?> GetBookByIsbnAsync(string isbn)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(isbn)) return null;

                var normalizedInput = NormalizeIsbn(isbn);

                // First, try exact match (fast path)
                var exact = await _BookMOCKData
                    .Find(Builders<BookMOCKData>.Filter.Eq(b => b.ISBN, isbn))
                    .FirstOrDefaultAsync();
                if (exact != null) return exact;

                // Fallback: scan and compare normalized strings (dataset expected to be modest)
                var all = await _BookMOCKData.Find(_ => true).ToListAsync();
                return all.FirstOrDefault(b => NormalizeIsbn(b.ISBN) == normalizedInput);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting book by ISBN: {ex.Message}");
                return null;
            }
        }

        // Find all MACData books by author name (case-insensitive contains)
        public async Task<List<BookMOCKData>> GetBooksByAuthorAsync(string authorName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(authorName)) return new List<BookMOCKData>();
                var regex = new BsonRegularExpression(RegexEscape(authorName), "i");
                var filter = Builders<BookMOCKData>.Filter.Regex(b => b.Author, regex);
                return await _BookMOCKData.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting books by author: {ex.Message}");
                return new List<BookMOCKData>();
            }
        }

        private static string RegexEscape(string input)
        {
            return System.Text.RegularExpressions.Regex.Escape(input ?? string.Empty);
        }

        private static string NormalizeIsbn(string value)
        {
            if (value == null) return string.Empty;
            var chars = value.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }

        public IMongoDatabase GetDatabase()
        {
            return _database;
        }
    }
}
