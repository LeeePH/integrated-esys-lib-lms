using MongoDB.Bson;
using MongoDB.Driver;
using SystemLibrary.Models;
using BCrypt.Net;

namespace SystemLibrary.Services
{
    public class AdminUserSeeder
    {
        private readonly IMongoDbService _mongoDbService;

        public AdminUserSeeder(IMongoDbService mongoDbService)
        {
            _mongoDbService = mongoDbService;
        }

        /// <summary>
        /// Ensures the admin user with email admin@gmail.com exists
        /// </summary>
        public async Task EnsureAdminUserAsync()
        {
            var usersCollection = _mongoDbService.GetCollection<User>("Users");
            var adminEmail = "admin@gmail.com";

            // Check if admin user already exists
            var existingAdmin = await usersCollection
                .Find(u => u.Email == adminEmail || (u.Role == "admin" && u.Email == adminEmail))
                .FirstOrDefaultAsync();

            if (existingAdmin != null)
            {
                Console.WriteLine($"[AdminUserSeeder] Admin user with email {adminEmail} already exists.");
                
                // Update password if needed (optional - uncomment if you want to reset password)
                // var passwordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
                // var update = Builders<User>.Update.Set(u => u.PasswordHash, passwordHash);
                // await usersCollection.UpdateOneAsync(u => u._id == existingAdmin._id, update);
                // Console.WriteLine($"[AdminUserSeeder] Admin password updated.");
                
                return;
            }

            // Create new admin user
            var adminUser = new User
            {
                _id = ObjectId.GenerateNewId(),
                StudentId = "ADMIN-001", // Admin identifier (Username property maps to this)
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Email = adminEmail,
                LastName = "Admin",
                FirstName = "System",
                MiddleName = null,
                Role = "admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IsRestricted = false,
                RestrictionReason = null,
                RestrictionDate = null,
                UpdatedAt = null,
                Course = "",
                HasPendingPenalties = false,
                TotalPendingPenalties = 0,
                PenaltyRestrictionDate = null,
                FailedLoginAttempts = 0,
                LockoutEndTime = null
            };

            await usersCollection.InsertOneAsync(adminUser);
            Console.WriteLine($"[AdminUserSeeder] Admin user created successfully with email: {adminEmail}");
        }
    }
}

