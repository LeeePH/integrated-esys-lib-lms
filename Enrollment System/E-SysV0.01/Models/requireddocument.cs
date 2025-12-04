using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace EnrollmentSystem.Models
{
    public class RequiredDocument
    {
        // ✅ MongoDB’s internal unique ID
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        // ✅ Custom numeric document ID (auto-increment handled manually)
        [BsonElement("document_id")]
        public int DocumentId { get; set; }

        [BsonElement("document_name")]
        public string DocumentName { get; set; } = string.Empty;

        [BsonElement("required_for")]
        public List<string> RequiredFor { get; set; } = new List<string>();  // Example: ["freshmen", "transferee"]

        // ✅ Optional metadata for auditing (added safely)
        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
