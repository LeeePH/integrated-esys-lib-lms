using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class BookMOCKData
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("isbn")]
        public string ISBN { get; set; } = string.Empty;

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("author")]
        public string Author { get; set; } = string.Empty;

        [BsonElement("publisher")]
        public string? Publisher { get; set; }

        [BsonElement("publication_date")]
        public string? PublicationDate { get; set; }

        [BsonElement("subject")]
        public string? Subject { get; set; }

        [BsonElement("classification_no")]
        public string? ClassificationNo { get; set; }
    }
}


