using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class PasswordResetToken
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("user_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("token")]
        public string Token { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("is_used")]
        public bool IsUsed { get; set; } = false;
    }
}