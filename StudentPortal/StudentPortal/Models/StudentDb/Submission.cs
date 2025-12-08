using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace StudentPortal.Models
{
    public class Submission
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string TaskId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;

        public bool Submitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsApproved { get; set; }
        public bool HasPassed { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string Grade { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;

        // File properties - remove duplicates and keep only one set
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    }
}