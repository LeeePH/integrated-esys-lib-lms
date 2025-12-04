using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace SystemLibrary.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("student_id")]
        public string StudentId { get; set; } = string.Empty;

        [BsonElement("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("last_name")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("middle_name")]
        public string? MiddleName { get; set; }

        [BsonElement("role")]
        public string Role { get; set; } = "student";

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("failed_login_attempts")]
        public int FailedLoginAttempts { get; set; } = 0;

        [BsonElement("lockout_end_time")]
        public DateTime? LockoutEndTime { get; set; }

        [BsonElement("is_restricted")]
        public bool IsRestricted { get; set; } = false;

        [BsonElement("restriction_reason")]
        public string? RestrictionReason { get; set; }

        [BsonElement("restriction_date")]
        public DateTime? RestrictionDate { get; set; }

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("course")]
        public string Course { get; set; } = string.Empty;

        [BsonElement("has_pending_penalties")]
        public bool HasPendingPenalties { get; set; } = false;

        [BsonElement("total_pending_penalties")]
        public decimal TotalPendingPenalties { get; set; } = 0;

        [BsonElement("penalty_restriction_date")]
        public DateTime? PenaltyRestrictionDate { get; set; }

        [BsonIgnore]
        public string FullName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LastName) && string.IsNullOrWhiteSpace(FirstName))
                    return StudentId; // Fallback to StudentId if no name

                // Only show FirstName and LastName (exclude MiddleName)
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName);
                if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName);

                return string.Join(" ", parts);
            }
        }

        // For backward compatibility (if you have old code using Username)
        [BsonIgnore]
        public string Username
        {
            get => StudentId;
            set => StudentId = value;
        }
    }
}