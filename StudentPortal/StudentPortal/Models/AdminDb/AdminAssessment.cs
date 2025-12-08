using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminAssessment
{
    public class AdminAssessment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
        public DateTime Deadline { get; set; }
        public string Status { get; set; } = "Active"; // Active, Draft, Archived
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AssessmentSubmission
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string AssessmentId { get; set; } = "";

        [BsonRepresentation(BsonType.ObjectId)]
        public string StudentId { get; set; } = "";

        public string StudentName { get; set; } = "";
        public string StudentEmail { get; set; } = "";
        public string Status { get; set; } = "Not Started"; // Not Started, In Progress, Submitted, Graded
        public DateTime? SubmittedAt { get; set; }
        public double? Score { get; set; }
        public double? MaxScore { get; set; }
        public string? Feedback { get; set; }
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
    public class AdminAssessmentPageViewModel
    {
        public AdminAssessment? Assessment { get; set; }
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string InstructorInitials { get; set; } = "";
        public List<string> RecentMaterials { get; set; } = new List<string>();
        public List<SubmissionViewModel> Submissions { get; set; } = new List<SubmissionViewModel>();
        public string AdminName { get; set; } = "";
        public string AdminInitials { get; set; } = "";
    }

    public class SubmissionViewModel
    {
        public string StudentName { get; set; } = "";
        public string Status { get; set; } = "Not Started";
        public DateTime? SubmittedAt { get; set; }
        public double? Score { get; set; }
    }
}