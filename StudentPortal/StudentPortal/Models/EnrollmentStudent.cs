using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models
{
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class EnrollmentStudent
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool FirstLogin { get; set; }

        [BsonElement("Email")]
        public string Email { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string ResetTokenHash { get; set; } = "";

        public DateTime? ResetTokenExpiryUtc { get; set; }

        // Optional fields that may or may not exist in the database
        public string? AccountStatus { get; set; }

        public string? LastEnrolledProgram { get; set; }
        public string? LastEnrolledYearLevel { get; set; }
        public string? LastEnrolledSemester { get; set; }
        public string? LastEnrolledAcademicYear { get; set; }
        public DateTime? LastEnrolledAt { get; set; }
        public DateTime? InactivatedAt { get; set; }
        public string? InactiveReason { get; set; }
        public string? InactiveTriggerTerm { get; set; }
    }
}

