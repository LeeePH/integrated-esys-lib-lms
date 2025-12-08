using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminTask
{
    public class AdminTaskViewModel
    {
        public string TaskId { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string InstructorInitials { get; set; } = "";
        public string TaskTitle { get; set; } = "";
        public string TaskDescription { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
        public DateTime PostedDate { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        public List<TaskSubmission> Submissions { get; set; } = new();
        public int TaskMaxGrade { get; set; } = 100;
    }

    public class TaskSubmission
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string StudentEmail { get; set; } = "";
        public bool Submitted { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsApproved { get; set; }
        public bool HasPassed { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public string Grade { get; set; } = "";
        public string Feedback { get; set; } = "";

        // File properties - these should be strings for display
        public string SubmittedFileName { get; set; } = "";
        public string SubmittedFileUrl { get; set; } = "";
        public string SubmittedFileSize { get; set; } = "";
    }

        // Database model
        public class TaskItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Attachments { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; }
        public string CreatedBy { get; set; } = "";
        public int MaxGrade { get; set; } = 100;
    }

    public class TaskReplyItem
    {
        public string AuthorEmail { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TaskCommentItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string TaskId { get; set; } = "";

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        public string AuthorEmail { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<TaskReplyItem> Replies { get; set; } = new();
    }
}
