using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    public class Author
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("name")]
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [BsonElement("alternate_names")]
        public List<string> AlternateNames { get; set; } = new List<string>();

        [BsonElement("bio")]
        [StringLength(1000)]
        public string? Bio { get; set; }

        [BsonElement("image")]
        public string? Image { get; set; }

        [BsonElement("first_publication_date")]
        public DateTime? FirstPublicationDate { get; set; }

        [BsonElement("last_publication_date")]
        public DateTime? LastPublicationDate { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}


