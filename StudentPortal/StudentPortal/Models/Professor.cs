using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models
{
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class Professor
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // Email field - matches actual database structure
        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        // Password hash field - matches actual database structure
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        // Name fields - matches actual database structure
        [BsonElement("givenName")]
        public string GivenName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("middleName")]
        public string? MiddleName { get; set; }

        [BsonElement("extension")]
        public string? Extension { get; set; }

        // Additional fields from database
        [BsonElement("programs")]
        public List<string>? Programs { get; set; }

        [BsonElement("bachelor")]
        public string? Bachelor { get; set; }

        [BsonElement("masters")]
        public string? Masters { get; set; }

        [BsonElement("phd")]
        public string? Phd { get; set; }

        [BsonElement("licenses")]
        public string? Licenses { get; set; }

        [BsonElement("facultyRole")]
        public string? FacultyRole { get; set; }

        [BsonElement("isTemporaryPassword")]
        public bool? IsTemporaryPassword { get; set; }

        [BsonElement("tempPasswordExpiresAt")]
        public DateTime? TempPasswordExpiresAt { get; set; }

        // Legacy field support (for backward compatibility)
        [BsonElement("Email")]
        public string EmailLegacy { get; set; } = string.Empty;

        [BsonElement("Password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("PasswordHash")]
        public string PasswordHashLegacy { get; set; } = string.Empty;

        [BsonElement("FullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("Role")]
        public string Role { get; set; } = "Professor";

        // Helper properties to get the actual values regardless of field name
        public string GetEmail() 
        {
            if (!string.IsNullOrEmpty(Email)) return Email;
            if (!string.IsNullOrEmpty(EmailLegacy)) return EmailLegacy;
            return string.Empty;
        }

        public string GetPasswordHash() 
        {
            if (!string.IsNullOrEmpty(PasswordHash)) return PasswordHash;
            if (!string.IsNullOrEmpty(PasswordHashLegacy)) return PasswordHashLegacy;
            if (!string.IsNullOrEmpty(Password)) return Password;
            return string.Empty;
        }

        public string GetFullName() 
        {
            // Build full name from givenName, lastName, middleName
            var nameParts = new List<string>();
            if (!string.IsNullOrEmpty(GivenName)) nameParts.Add(GivenName);
            if (!string.IsNullOrEmpty(MiddleName)) nameParts.Add(MiddleName);
            if (!string.IsNullOrEmpty(LastName)) nameParts.Add(LastName);
            
            if (nameParts.Count > 0)
                return string.Join(" ", nameParts);
            
            // Fallback to legacy FullName field
            if (!string.IsNullOrEmpty(FullName)) return FullName;
            
            return string.Empty;
        }

        public string GetRole() 
        {
            if (!string.IsNullOrEmpty(Role)) return Role;
            return "Professor"; // Default role
        }
    }
}

