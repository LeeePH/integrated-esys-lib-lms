using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class Penalty
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("user_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; } = string.Empty;

        [BsonElement("student_name")]
        public string StudentName { get; set; } = string.Empty;

        [BsonElement("reservation_id")]
        public ObjectId ReservationId { get; set; }

        [BsonElement("book_id")]
        public ObjectId BookId { get; set; }

        [BsonElement("book_title")]
        public string BookTitle { get; set; } = string.Empty;

        [BsonElement("penalty_type")]
        public string PenaltyType { get; set; } = string.Empty; // "Late", "Damage", "Lost"

        [BsonElement("amount")]
        public decimal Amount { get; set; }

        [BsonElement("penalty_amount")]
        public decimal PenaltyAmount { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("is_paid")]
        public bool IsPaid { get; set; } = false;

        [BsonElement("payment_date")]
        public DateTime? PaymentDate { get; set; }

        [BsonElement("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("created_by")]
        public ObjectId? CreatedBy { get; set; }

        [BsonElement("remarks")]
        public string Remarks { get; set; } = string.Empty;
    }
}
