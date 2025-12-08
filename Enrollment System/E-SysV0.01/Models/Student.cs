using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace E_SysV0._01.Models
{
    [BsonIgnoreExtraElements] // Ignore extra fields like "AccountStatus" that may exist in the database
    public class Student
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool FirstLogin { get; set; }

        public string Email { get; set; } = string.Empty;

        // Freshman, Regular, Irregular, Shifter, Returnee, Transferee
        public string Type { get; set; } = string.Empty;

        // Password reset support
        // SHA256 hex of the one-time token sent to the user
        public string ResetTokenHash { get; set; } = "";

        // UTC expiry for the reset token; null when no active token
        public DateTime? ResetTokenExpiryUtc { get; set; }
    }
}