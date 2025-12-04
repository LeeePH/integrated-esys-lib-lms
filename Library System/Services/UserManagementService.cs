using MongoDB.Driver;
using MongoDB.Bson;
using SystemLibrary.Models;
using SystemLibrary.ViewModels;
using BCrypt.Net;
using System.Net.Mail;

namespace SystemLibrary.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Reservation> _reservations;
        private readonly IMongoCollection<StudentProfile> _studentProfiles;

        public UserManagementService(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("Users");
            _reservations = database.GetCollection<Reservation>("Reservations");
            _studentProfiles = database.GetCollection<StudentProfile>("StudentProfiles");
        }

        public async Task<List<User>> GetLibraryStaffAsync()
        {
            var filter = Builders<User>.Filter.In(u => u.Role, new[] { "admin", "librarian" });
            return await _users.Find(filter).ToListAsync();
        }

        public async Task<List<User>> GetStudentsAsync()
        {
            var filter = Builders<User>.Filter.Eq(u => u.Role, "student");
            return await _users.Find(filter).ToListAsync();
        }

        public async Task<bool> AddUserAsync(CreateUserViewModel model)
        {
            try
            {
                var normalizedUsername = (model.Username ?? string.Empty).Trim().ToLowerInvariant();
                var normalizedEmail = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
                var normalizedRole = (model.Role ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(normalizedEmail))
                {
                    var nameCandidate = (model.Name ?? string.Empty).Trim();
                    var usernameCandidate = (model.Username ?? string.Empty).Trim();

                    if (nameCandidate.Contains("@"))
                    {
                        normalizedEmail = nameCandidate.ToLowerInvariant();
                    }
                    else if (usernameCandidate.Contains("@"))
                    {
                        normalizedEmail = usernameCandidate.ToLowerInvariant();
                    }
                }

                if (string.IsNullOrWhiteSpace(normalizedUsername))
                {
                    return false;
                }

                // Check if username or email already exists (stored as StudentId / Email)
                var existingUser = await _users
                    .Find(u => u.StudentId == normalizedUsername || (!string.IsNullOrEmpty(normalizedEmail) && u.Email == normalizedEmail))
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return false;
                }

                // Hash the password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password);

                var firstName = model.Name?.Split(' ').FirstOrDefault() ?? string.Empty;
                var lastName = string.Empty;

                if (!string.IsNullOrWhiteSpace(model.Name))
                {
                    var firstSpaceIndex = model.Name.IndexOf(' ');
                    if (firstSpaceIndex > 0 && firstSpaceIndex < model.Name.Length - 1)
                    {
                        lastName = model.Name.Substring(firstSpaceIndex + 1);
                    }
                }

                var newUser = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    StudentId = normalizedUsername,
                    PasswordHash = hashedPassword,
                    Email = normalizedEmail,
                    Role = normalizedRole,
                    IsActive = true,
                    IsRestricted = false,
                    CreatedAt = DateTime.UtcNow,
                    Course = model.Course ?? ""
                };

                await _users.InsertOneAsync(newUser);

                // if the user is a student, create a student profile
                if (normalizedRole == "student")
                {
                    var studentProfile = new StudentProfile
                    {
                        UserId = newUser._id,
                        StudentNumber = model.StudentNumber ?? "",
                        ContactNumber = model.ContactNumber ?? "",
                        Course = model.Course ?? "",
                        YearLevel = model.YearLevel ?? "",
                        Program = model.Program ?? "",
                        Department = model.Department ?? "",
                        IsEnrolled = true,
                        IsFlagged = false,
                        BorrowingLimit = 3,
                        TotalPenalties = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        FullName = model.Name
                    };

                    await _studentProfiles.InsertOneAsync(studentProfile);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdatePasswordAsync(string userId, string oldPassword, string newPassword)
        {
            try
            {
                var user = await _users.Find(u => u._id == ObjectId.Parse(userId)).FirstOrDefaultAsync();
                if (user == null)
                {
                    return false;
                }

                // Verify old password with PasswordHash property
                if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
                {
                    return false;
                }

                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                var update = Builders<User>.Update
                    .Set(u => u.PasswordHash, hashedPassword)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }


        public async Task<bool> DeactivateUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update
                    .Set(u => u.IsActive, false)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ActivateUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update
                    .Set(u => u.IsActive, true)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RestrictUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update
                    .Set(u => u.IsRestricted, true)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UnrestrictUserAsync(string userId)
        {
            try
            {
                var update = Builders<User>.Update
                    .Set(u => u.IsRestricted, false)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                if (!ObjectId.TryParse(userId, out var objId))
                {
                    return false;
                }
                var result = await _users.DeleteOneAsync(u => u._id == objId);
                return result.DeletedCount > 0;
            }
            catch (Exception ex)
            {
                // Log ex
                throw;
            }
        }


        public async Task<User> GetUserByIdAsync(string userId)
        {
            try
            {
                return await _users.Find(u => u._id == ObjectId.Parse(userId)).FirstOrDefaultAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> CanDeleteUserAsync(string userId)
        {
            try
            {
                // Check if user has any active reservations
                var activeReservations = await _reservations.Find(r =>
                    r.UserId == userId &&
                    (r.Status == "pending" || r.Status == "approved")
                ).CountDocumentsAsync();

                return activeReservations == 0;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> AdminResetPasswordAsync(string userId, string newPassword)
        {
            try
            {
                var user = await _users.Find(u => u._id == ObjectId.Parse(userId)).FirstOrDefaultAsync();
                if (user == null)
                {
                    return false;
                }

                // Hash new password
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

                // Update PasswordHash without checking old password
                var update = Builders<User>.Update
                    .Set(u => u.PasswordHash, hashedPassword)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow);

                var result = await _users.UpdateOneAsync(
                    u => u._id == ObjectId.Parse(userId),
                    update
                );

                return result.ModifiedCount > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}

