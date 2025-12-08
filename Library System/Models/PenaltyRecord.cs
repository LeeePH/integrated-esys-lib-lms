using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class PenaltyRecord
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("user_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("book_title")]
        public string BookTitle { get; set; } = "";

        [BsonElement("penalty_amount")]
        public decimal PenaltyAmount { get; set; }

        [BsonElement("reason")]
        public string Reason { get; set; } = "";

        [BsonElement("condition")]
        public string Condition { get; set; } = "Good"; // "Good", "Damage", "Lost"

        [BsonElement("days_late")]
        public int DaysLate { get; set; }

        [BsonElement("is_paid")]
        public bool IsPaid { get; set; } = false;

        [BsonElement("paid_at")]
        public DateTime? PaidAt { get; set; }

        [BsonElement("payment_reference")]
        public string PaymentReference { get; set; } = "";

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper properties for UI
        [BsonIgnore]
        public string StatusText => IsPaid ? "Paid" : "Unpaid";

        [BsonIgnore]
        public string StatusClass => IsPaid ? "success" : "danger";

        [BsonIgnore]
        public string ConditionBadgeClass => Condition switch
        {
            "Good" => "badge-success",
            "Damage" => "badge-warning",
            "Lost" => "badge-danger",
            _ => "badge-secondary"
        };
    }
}