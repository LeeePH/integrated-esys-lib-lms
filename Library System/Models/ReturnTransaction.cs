using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    public class ReturnTransaction
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("reservation_id")]
        public ObjectId ReservationId { get; set; }

        [BsonElement("book_id")]
        public ObjectId BookId { get; set; }

        [BsonElement("student_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; } = "";

        [BsonElement("student_name")]
        public string StudentName { get; set; } = "";

        [BsonElement("book_title")]
        public string BookTitle { get; set; } = "";

        [BsonElement("borrow_date")]
        public DateTime BorrowDate { get; set; }

        [BsonElement("due_date")]
        public DateTime DueDate { get; set; }

        [BsonElement("return_date")]
        public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

        [BsonElement("days_late")]
        public int DaysLate { get; set; }

        [BsonElement("late_fees")]
        [Range(0, double.MaxValue, ErrorMessage = "Late fees cannot be negative")]
        public decimal LateFees { get; set; }

        [BsonElement("book_condition")]
        [Required(ErrorMessage = "Book condition is required")]
        public string BookCondition { get; set; } = "Good"; // Good, Damaged-Minor, Damaged-Moderate, Damaged-Major, Lost

        [BsonElement("damage_type")]
        public string? DamageType { get; set; } // Minor, Moderate, Major (only for Damage condition)

        [BsonElement("damage_penalty")]
        [Range(0, double.MaxValue, ErrorMessage = "Damage penalty cannot be negative")]
        public decimal DamagePenalty { get; set; }

        [BsonElement("penalty_amount")]
        [Range(0, double.MaxValue, ErrorMessage = "Penalty amount cannot be negative")]
        public decimal PenaltyAmount { get; set; }

        [BsonElement("total_penalty")]
        [Range(0, double.MaxValue, ErrorMessage = "Total penalty cannot be negative")]
        public decimal TotalPenalty { get; set; }

        [BsonElement("remarks")]
        [StringLength(500, ErrorMessage = "Remarks cannot exceed 500 characters")]
        public string? Remarks { get; set; }

        [BsonElement("processed_by")]
        public ObjectId? ProcessedBy { get; set; }

        [BsonElement("payment_status")]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Paid

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Helper properties
        [BsonIgnore]
        public bool IsOverdue => DaysLate > 0;

        [BsonIgnore]
        public string StatusColor => BookCondition switch
        {
            "Good" => "success",
            "Damaged-Minor" => "warning",
            "Damaged-Moderate" => "warning",
            "Damaged-Major" => "warning",
            "Lost" => "danger",
            _ => "secondary"
        };

        [BsonIgnore]
        public string DisplayId => _id.ToString().Substring(0, 8) + "...";

        [BsonIgnore]
        public string DamageTypeDisplay => BookCondition.StartsWith("Damaged-")
            ? BookCondition.Replace("Damaged-", "") + " Damage"
            : BookCondition;

        // Additional properties for display
        [BsonIgnore]
        public string ISBN { get; set; } = "";

        [BsonIgnore]
        public string ClassificationNo { get; set; } = "";
    }
}