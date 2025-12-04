using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models.Library
{
    public class Reservation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; } = "";

        [BsonElement("user_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; } = "";

        [BsonElement("book_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string BookId { get; set; } = "";

        [BsonElement("book_title")]
        public string BookTitle { get; set; } = "";

        [BsonElement("student_number")]
        public string StudentNumber { get; set; } = "";

        [BsonElement("reservation_date")]
        public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

        [BsonElement("status")]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Borrowed, Returned, Cancelled

        [BsonElement("approval_date")]
        public DateTime? ApprovalDate { get; set; }

        [BsonElement("due_date")]
        public DateTime? DueDate { get; set; }

        [BsonElement("reservation_type")]
        public string ReservationType { get; set; } = "Reserve"; // Reserve or Waitlist
    }
}

