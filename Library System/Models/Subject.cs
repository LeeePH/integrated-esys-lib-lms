using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    public class Subject
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("name")]
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [BsonElement("parent_subject_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId? ParentSubjectId { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}


