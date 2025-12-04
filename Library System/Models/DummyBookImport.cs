using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    // Staging document for quick adds: input ISBN + quantity; rest auto-filled from MOCK data
    public class DummyBookImport
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("isbn")]
        [Required]
        [StringLength(20)]
        public string ISBN { get; set; } = string.Empty;

        [BsonElement("quantity")]
        [Range(1, 1000)]
        public int Quantity { get; set; } = 1;

        [BsonElement("status")] // pending, completed, failed
        public string Status { get; set; } = "pending";

        [BsonElement("notes")]
        [StringLength(300)]
        public string? Notes { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }
}


