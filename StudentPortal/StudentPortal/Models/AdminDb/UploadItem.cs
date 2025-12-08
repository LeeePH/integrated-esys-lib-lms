using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminMaterial
{
    public class UploadItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";

        // REMOVE BsonRepresentation - allow any string value including empty
        public string ContentId { get; set; } = "";

        public string UploadedBy { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public string FileType { get; set; } = "";
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

        public class ContentItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ClassId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";  // material|task|assessment|announcement
        public string IconClass { get; set; } = "fa-solid fa-file";
        public string MetaText { get; set; } = "";
        public string TargetUrl { get; set; } = "";
        public string UploadedBy { get; set; } = "Unknown";
        public string Description { get; set; } = "";
        public bool HasUrgency { get; set; } = false;
        public string UrgencyColor { get; set; } = "yellow";
        public string LinkUrl { get; set; } = "";
        public List<string> Attachments { get; set; } = new List<string>();
        public DateTime? Deadline { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int MaxGrade { get; set; } = 100;

        // Material-specific properties
        public string FileUrl { get; set; } = "";
        public string FileType { get; set; } = "";
        public long FileSize { get; set; }
    }
}
