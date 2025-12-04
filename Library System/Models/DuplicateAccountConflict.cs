using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class DuplicateAccountConflict
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("conflict_type")]
        public string ConflictType { get; set; } = string.Empty; // "DuplicateStudent", "StaffAsStudent"

        [BsonElement("user_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; } = string.Empty;

        [BsonElement("full_name")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("duplicate_user_ids")]
        public List<ObjectId> DuplicateUserIds { get; set; } = new List<ObjectId>();

        [BsonElement("employee_id")]
        public string? EmployeeId { get; set; }

        [BsonElement("conflict_details")]
        public string ConflictDetails { get; set; } = string.Empty;

        [BsonElement("detected_at")]
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("is_resolved")]
        public bool IsResolved { get; set; } = false;

        [BsonElement("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [BsonElement("resolved_by")]
        public ObjectId? ResolvedBy { get; set; }

        [BsonElement("resolution_notes")]
        public string? ResolutionNotes { get; set; }
    }
}

