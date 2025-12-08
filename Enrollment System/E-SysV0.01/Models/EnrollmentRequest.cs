// Add this XML documentation comment at the top of the EnrollmentRequest class:

/// <summary>
/// Enrollment Request Model
/// 
/// Type values:
/// - "Freshmen" - New 1st year students (1st semester)
/// - "Transferee" - Students transferring from other schools
/// - "Shifter" - Students changing programs within the school
/// - "Returnee" - Students returning after absence
/// - "Regular" - Continuing students (same year, next semester)
/// - "Irregular" - Students with failed subjects
/// - "2nd Year-Regular" - Students advancing to 2nd year (no failed subjects)
/// - "2nd Year-Irregular" - Students advancing to 2nd year (with failed subjects)
/// 
/// Status values:
/// - "Account Pending" - Initial freshmen submission
/// - "1st Sem Pending" - Awaiting admin review for 1st semester
/// - "2nd Sem Pending" - Awaiting admin review for 2nd semester
/// - "On Hold" - Temporarily paused
/// - "Rejected" - Declined enrollment
/// - "Enrolled - 1st Semester" - Accepted 1st semester
/// - "Enrolled - 2nd Semester" - Accepted 2nd semester
/// - "Enrolled - 2nd Year 1st Semester" - Accepted 2nd year 1st semester
/// - "Enrolled - 2nd Year 2nd Semester" - Accepted 2nd year 2nd semester
/// (Pattern continues for 3rd and 4th year)
/// 
/// ExtraFields structure for 2nd Year enrollment:
/// - "SubjectRemarks.CC101" = "pass" (1st Year 1st Sem)
/// - "SubjectRemarks.2ndSem.CC103" = "fail" (1st Year 2nd Sem)
/// - "SecondYearEligibility.IM101" = "Can enroll" (Calculated eligibility)
/// </summary>
/// 
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    [BsonIgnoreExtraElements] // Add this
    public class EnrollmentRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string FullName { get; set; } = "";

        public string? Program { get; set; }
        public string Type { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public string? Reason { get; set; }
        public string? Notes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public Dictionary<string, string>? ExtraFields { get; set; }
        public string? EditToken { get; set; }
        public DateTime? EditTokenExpires { get; set; }
        public Dictionary<string, string>? DocumentFlags { get; set; }
        public Dictionary<string, string>? SecondSemesterEligibility { get; set; }
    }
}