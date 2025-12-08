using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models.Library
{
    [BsonIgnoreExtraElements] // Ignore extra fields that might exist in the database
    public class Book
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; } = "";

        [BsonElement("author")]
        public string Author { get; set; } = "";

        [BsonElement("publisher")]
        public string Publisher { get; set; } = "";

        [BsonElement("classification_no")]
        public string ClassificationNo { get; set; } = "";

        [BsonElement("isbn")]
        public string ISBN { get; set; } = "";

        [BsonElement("subject")]
        public string Subject { get; set; } = "";

        [BsonElement("image")]
        public string Image { get; set; } = "/images/default-book.png";

        [BsonElement("total_copies")]
        public int TotalCopies { get; set; } = 1;

        [BsonElement("available_copies")]
        public int AvailableCopies { get; set; } = 1;

        [BsonElement("publication_date")]
        public DateTime? PublicationDate { get; set; }

        [BsonElement("restrictions")]
        public string? Restrictions { get; set; }

        [BsonElement("is_reference_only")]
        public bool IsReferenceOnly { get; set; } = false;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [BsonIgnore]
        public bool IsAvailable => AvailableCopies > 0 && !IsReferenceOnly;
    }
}

    