using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace E_SysV0._01.Models
{
    [BsonIgnoreExtraElements]
    public class ShifterEnrollmentRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Required]
        public string StudentUsername { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Personal Info (auto-filled from Student record - readonly on form)
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public string Sex { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;

        // Academic Info
        [Required]
        public string CourseLastEnrolled { get; set; } = string.Empty; // auto-filled (BSIT/BSENT)

        [Required]
        public string CourseApplyingToShift { get; set; } = string.Empty; // editable dropdown

        public int TotalUnitsEarned { get; set; } // auto-calculated from passed subjects
        public int TotalUnitsFailed { get; set; } // auto-calculated from failed subjects

        // Reason for Shifting
        [Required]
        public string ReasonForShifting { get; set; } = string.Empty;

        // Documents (paths to uploaded files)
        public string? EndorsementLetterPath { get; set; }
        public string? LibraryClearancePath { get; set; }

        // Status: Pending, Accepted, Rejected, On-Hold
        public string Status { get; set; } = "Pending";

        public string CurrentSemester { get; set; } = string.Empty; // When submitted
        public string CurrentAcademicYear { get; set; } = string.Empty;

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedDate { get; set; }

        // Admin notes
        public string? AdminNotes { get; set; }

        // Token for accessing the shifter form
        public string? AccessToken { get; set; }
        public DateTime? AccessTokenExpires { get; set; }
    }
}