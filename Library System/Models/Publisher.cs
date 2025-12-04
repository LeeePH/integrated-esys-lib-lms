using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    public class Publisher
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("name")]
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [BsonElement("address")]
        [StringLength(300)]
        public string? Address { get; set; }

        [BsonElement("contact_email")]
        [StringLength(150)]
        public string? ContactEmail { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}


