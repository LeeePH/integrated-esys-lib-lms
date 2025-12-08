using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
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

        [BsonElement("book_copy_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? BookCopyId { get; set; } // Specific copy being borrowed

        [BsonElement("copy_identifier")]
        public string? CopyIdentifier { get; set; } // Human-readable copy ID like "SDS-0232"

        [BsonElement("book_title")]
        public string BookTitle { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; }

        [BsonElement("reservation_date")]
        public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

        [BsonElement("status")]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Borrowed, Returned, Cancelled

        [BsonElement("approval_date")]
        public DateTime? ApprovalDate { get; set; }

        [BsonElement("due_date")]
        public DateTime? DueDate { get; set; }

        [BsonElement("return_date")]
        public DateTime? ReturnDate { get; set; }

        [BsonElement("approved_by")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ApprovedBy { get; set; }

        [BsonElement("rejected_by")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? RejectedBy { get; set; }

        [BsonElement("reservation_type")]
        public string ReservationType { get; set; } = "Reserve"; // Reserve or Waitlist

        [BsonElement("notes")]
        public string? Notes { get; set; }

        // Navigation properties (not stored in DB)
        [BsonIgnore]
        public Book? Book { get; set; }

        [BsonElement("full_name")]
        public string FullName { get; set; } = string.Empty;
        [BsonIgnore]
        public User? User { get; set; }

        [BsonIgnore]
        public bool IsActive => Status == "Approved" || Status == "Borrowed";

        // Helper properties for the new workflow
        [BsonIgnore]
        public bool IsApprovalExpired => Status == "Approved" && ApprovalDate.HasValue && 
            DateTime.UtcNow > ApprovalDate.Value.AddMinutes(2);

        [BsonIgnore]
        public int HoursRemainingForPickup => Status == "Approved" && ApprovalDate.HasValue ? 
            Math.Max(0, (int)(ApprovalDate.Value.AddMinutes(2) - DateTime.UtcNow).TotalHours) : 0;

        [BsonIgnore]
        public int MinutesRemainingForPickup => Status == "Approved" && ApprovalDate.HasValue ?
            Math.Max(0, (int)(ApprovalDate.Value.AddMinutes(2) - DateTime.UtcNow).TotalMinutes) : 0;

        [BsonIgnore]
        public int DaysRemaining => DueDate.HasValue ? 
            Math.Max(0, (DueDate.Value - DateTime.UtcNow).Days) : 0;

        [BsonIgnore]
        public bool IsOverdue => DueDate.HasValue && DateTime.UtcNow > DueDate.Value && Status == "Borrowed";
        
        [BsonElement("borrow_type")]
        [BsonIgnoreIfNull]
        public string BorrowType { get; set; } // "ONLINE", "WALK IN"

        [BsonElement("pickup_reminder_sent")]
        [BsonIgnoreIfNull]
        public bool? PickupReminderSent { get; set; } = false;

        [BsonElement("inventory_hold_active")]
        public bool InventoryHoldActive { get; set; } = false;

        [BsonElement("is_suspicious")]
        public bool IsSuspicious { get; set; } = false;

        [BsonElement("suspicious_reason")]
        [BsonIgnoreIfNull]
        public string? SuspiciousReason { get; set; }

        [BsonElement("suspicious_detected_at")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonIgnoreIfNull]
        public DateTime? SuspiciousDetectedAt { get; set; }
    }
}