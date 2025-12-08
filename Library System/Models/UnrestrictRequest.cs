using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class UnrestrictRequest
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        [BsonElement("studentName")]
        public string StudentName { get; set; }

        [BsonElement("studentNumber")]
        public string StudentNumber { get; set; }

        [BsonElement("requestedBy")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string RequestedBy { get; set; }

        [BsonElement("requestedByName")]
        public string RequestedByName { get; set; }

        [BsonElement("requestedByRole")]
        public string RequestedByRole { get; set; }

        [BsonElement("reason")]
        public string Reason { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [BsonElement("processedBy")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProcessedBy { get; set; }

        [BsonElement("processedByName")]
        public string ProcessedByName { get; set; }

        [BsonElement("processedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ProcessedAt { get; set; }

        [BsonElement("adminNotes")]
        public string AdminNotes { get; set; }

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
