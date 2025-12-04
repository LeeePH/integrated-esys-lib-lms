using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace StudentPortal.Models.AdminDb
{
    public class JoinRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } // ✅ string

        public string StudentName { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // "Pending" or "Approved"

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; } // Set when status changes to "Approved"
    }
}
