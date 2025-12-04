using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace SystemLibrary.Services
{
    // Student model from enrollment system
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class EnrollmentStudent
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("FirstLogin")]
        public bool FirstLogin { get; set; }

        [BsonElement("Type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("ResetTokenHash")]
        public string ResetTokenHash { get; set; } = string.Empty;

        [BsonElement("ResetTokenExpiryUtc")]
        public DateTime? ResetTokenExpiryUtc { get; set; }
    }

    // EnrollmentRequest model from enrollment system
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class EnrollmentRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("FullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("Program")]
        public string? Program { get; set; }

        [BsonElement("Type")]
        public string Type { get; set; } = string.Empty;

        [BsonElement("Status")]
        public string Status { get; set; } = "Pending";

        [BsonElement("EmergencyContactName")]
        public string? EmergencyContactName { get; set; }

        [BsonElement("EmergencyContactPhone")]
        public string? EmergencyContactPhone { get; set; }

        [BsonElement("ExtraFields")]
        public Dictionary<string, string>? ExtraFields { get; set; }
    }

    public interface IEnrollmentSystemService
    {
        Task<EnrollmentStudent?> GetStudentByEmailAsync(string email);
        Task<EnrollmentStudent?> GetStudentByUsernameAsync(string username);
        Task<EnrollmentRequest?> GetLatestEnrollmentRequestByEmailAsync(string email);
    }

    public class EnrollmentSystemService : IEnrollmentSystemService
    {
        private readonly IMongoCollection<EnrollmentStudent> _students;
        private readonly IMongoCollection<EnrollmentRequest> _enrollmentRequests;

        public EnrollmentSystemService(IConfiguration configuration)
        {
            var connectionString = configuration["EnrollmentSystem:Mongo:ConnectionString"];
            var databaseName = configuration["EnrollmentSystem:Mongo:Database"] ?? "ESys";
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("EnrollmentSystem:Mongo:ConnectionString is not configured in appsettings.json");
            }

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _students = database.GetCollection<EnrollmentStudent>("students");
            _enrollmentRequests = database.GetCollection<EnrollmentRequest>("enrollmentRequests");

            try
            {
                // Optional connectivity test - if it fails, log and allow the rest of the system to keep working.
                database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                Console.WriteLine($"✅ Connected to Enrollment System MongoDB: {databaseName}");
            }
            catch (Exception ex)
            {
                // Do NOT crash the whole app if enrollment DB/DNS is unavailable.
                Console.WriteLine($"❌ Enrollment System MongoDB connection failed (service will be treated as unavailable): {ex.Message}");
                // All public methods already have try/catch and will simply return null if the DB can't be reached.
            }
        }

        public async Task<EnrollmentStudent?> GetStudentByEmailAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    Console.WriteLine("[EnrollmentSystemService] Email is empty");
                    return null;
                }

                var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
                Console.WriteLine($"[EnrollmentSystemService] Searching for student with email: {normalized}");
                
                var student = await _students.Find(s => s.Email == normalized).FirstOrDefaultAsync();
                
                if (student != null)
                {
                    Console.WriteLine($"[EnrollmentSystemService] Found student: {student.Username} (ID: {student.Id})");
                }
                else
                {
                    Console.WriteLine($"[EnrollmentSystemService] Student not found with email: {normalized}");
                    
                    // Debug: List a few students to check if collection has data
                    var count = await _students.CountDocumentsAsync(_ => true);
                    Console.WriteLine($"[EnrollmentSystemService] Total students in collection: {count}");
                    
                    if (count > 0)
                    {
                        var sample = await _students.Find(_ => true).Limit(5).ToListAsync();
                        Console.WriteLine($"[EnrollmentSystemService] Sample student emails: {string.Join(", ", sample.Select(s => s.Email))}");
                    }
                }
                
                return student;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnrollmentSystemService] Error fetching student from enrollment system: {ex.Message}");
                Console.WriteLine($"[EnrollmentSystemService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public async Task<EnrollmentStudent?> GetStudentByUsernameAsync(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                    return null;
                    
                return await _students.Find(s => s.Username == username).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching student from enrollment system: {ex.Message}");
                return null;
            }
        }

        public async Task<EnrollmentRequest?> GetLatestEnrollmentRequestByEmailAsync(string email)
        {
            try
            {
                var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
                var filter = Builders<EnrollmentRequest>.Filter.Eq(r => r.Email, normalized);
                return await _enrollmentRequests
                    .Find(filter)
                    .SortByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching enrollment request: {ex.Message}");
                return null;
            }
        }
    }
}

