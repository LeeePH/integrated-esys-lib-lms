using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("userId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        [BsonElement("type")]
        public string Type { get; set; } // RESERVATION_CREATED, RESERVATION_APPROVED, BOOK_OVERDUE, etc.

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("message")]
        public string Message { get; set; }

        [BsonElement("bookTitle")]
        public string BookTitle { get; set; }

        [BsonElement("reservationId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ReservationId { get; set; }

        [BsonElement("isRead")]
        public bool IsRead { get; set; } = false;

        [BsonElement("readAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ReadAt { get; set; }

        [BsonElement("isArchived")]
        public bool IsArchived { get; set; } = false;

        [BsonElement("archivedAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ArchivedAt { get; set; }

        [BsonElement("createdAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper properties for UI styling
        [BsonIgnore]
        public string Icon
        {
            get
            {
                return Type switch
                {
                    "RESERVATION_CREATED" => "fa-calendar-check",
                    "NEW_RESERVATION" => "fa-book",
                    "RESERVATION_APPROVED" => "fa-check-circle",
                    "RESERVATION_REJECTED" => "fa-times-circle",
                    "SUSPICIOUS_RESERVATION" => "fa-user-shield",
                    "BOOK_DUE_SOON" => "fa-clock",
                    "BOOK_OVERDUE" => "fa-exclamation-triangle",
                    "BOOK_RETURNED" => "fa-undo",
                    "RENEWAL_REQUESTED" => "fa-sync",
                    "ACCOUNT_UNRESTRICTED" => "fa-user-check",
                    "PENALTY_ISSUED" => "fa-exclamation-triangle",
                    "PENALTY_PAID" => "fa-check-circle",
                    _ => "fa-bell"
                };
            }
        }

        [BsonIgnore]
        public string CssClass
        {
            get
            {
                return Type switch
                {
                    "RESERVATION_APPROVED" => "success",
                    "BOOK_RETURNED" => "success",
                    "ACCOUNT_UNRESTRICTED" => "success",
                    "PENALTY_PAID" => "success",
                    "NEW_RESERVATION" => "info",
                    "SUSPICIOUS_RESERVATION" => "danger",
                    "RESERVATION_REJECTED" => "danger",
                    "BOOK_OVERDUE" => "danger",
                    "PENALTY_ISSUED" => "danger",
                    "BOOK_DUE_SOON" => "warning",
                    "RENEWAL_REQUESTED" => "info",
                    _ => "info"
                };
            }
        }
    }
}