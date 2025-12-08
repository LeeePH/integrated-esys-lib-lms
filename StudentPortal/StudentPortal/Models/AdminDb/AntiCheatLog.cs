using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace StudentPortal.Models.AdminDb
{
    public class AntiCheatLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string ClassId { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        public string ResultId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public int EventCount { get; set; }
        public int EventDuration { get; set; }
        public string Severity { get; set; } = "low";
        public bool Flagged { get; set; }
        public DateTime LogTimeUtc { get; set; } = DateTime.UtcNow;
    }
}
