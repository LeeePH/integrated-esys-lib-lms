using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    [BsonIgnoreExtraElements]
    public class EnrollmentArchive
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = "";

        // Reference to original request
        public string OriginalRequestId { get; set; } = "";

        // Core student info
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Program { get; set; }
        public string Type { get; set; } = "";

        // Status at time of archiving
        public string Status { get; set; } = "";
        public string StatusAtArchive { get; set; } = "";

        public string? Reason { get; set; }
        public string? Notes { get; set; }

        // Timestamps
        public DateTime SubmittedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public DateTime ArchivedAt { get; set; }

        // Archive metadata
        public string? ArchiveReason { get; set; }
        public string AcademicYear { get; set; } = "";
        public string Semester { get; set; } = "";

        // ✅ NEW: Emergency contact info
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Preserved data from original request
        public Dictionary<string, string>? ExtraFields { get; set; }
        public Dictionary<string, string>? DocumentFlags { get; set; }

        // ✅ NEW: Eligibility data
        public Dictionary<string, string>? SecondSemesterEligibility { get; set; }
    }
}