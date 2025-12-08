using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models
{
    [BsonIgnoreExtraElements] // Ignore extra fields from professorDB (e.g., "Subjects")
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("Password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("OTP")]
        public string OTP { get; set; } = string.Empty;

        [BsonElement("IsVerified")]
        public bool IsVerified { get; set; } = false;

        [BsonElement("FullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("LastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("FirstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("MiddleName")]
        public string MiddleName { get; set; } = string.Empty;

        [BsonElement("Role")]
        public string Role { get; set; } = "Student"; // Default role

        public int? FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutEndTime { get; set; }


        [BsonElement("JoinedClasses")]
        public List<string> JoinedClasses { get; set; } = new List<string>();

        [BsonElement("EnrollmentId")]
        public string EnrollmentId { get; set; } = string.Empty;

        [BsonElement("EnrollmentUsername")]
        public string EnrollmentUsername { get; set; } = string.Empty;
    }
}
