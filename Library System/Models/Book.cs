using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.Models
{
    public class Book
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId _id { get; set; }

        [BsonElement("title")]
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = "";

        [BsonElement("author")]
        [Required(ErrorMessage = "Author is required")]
        [StringLength(100, ErrorMessage = "Author name cannot exceed 100 characters")]
        public string Author { get; set; } = "";

        [BsonElement("publisher")]
        [StringLength(100, ErrorMessage = "Publisher name cannot exceed 100 characters")]
        public string Publisher { get; set; } = "";

        [BsonElement("classification_no")]
        [StringLength(50, ErrorMessage = "Classification number cannot exceed 50 characters")]
        public string ClassificationNo { get; set; } = "";

        [BsonElement("isbn")]
        [StringLength(20, ErrorMessage = "ISBN cannot exceed 20 characters")]
        public string ISBN { get; set; } = "";

        [BsonElement("subject")]
        [Required(ErrorMessage = "Subject is required")]
        [StringLength(50, ErrorMessage = "Subject cannot exceed 50 characters")]
        public string Subject { get; set; } = "";

        [BsonElement("image")]
        public string Image { get; set; } = "/images/default-book.png";

        [BsonElement("total_copies")]
        [Required(ErrorMessage = "Total copies is required")]
        [Range(1, 1000, ErrorMessage = "Total copies must be between 1 and 1000")]
        public int TotalCopies { get; set; } = 1;

        [BsonElement("available_copies")]
        [Range(0, int.MaxValue, ErrorMessage = "Available copies cannot be negative")]
        public int AvailableCopies { get; set; } = 1;

        [BsonElement("publication_date")]
        [DataType(DataType.Date)]
        public DateTime? PublicationDate { get; set; }

        [BsonElement("restrictions")]
        [StringLength(300, ErrorMessage = "Restrictions text cannot exceed 300 characters")]
        public string? Restrictions { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("copy_management_enabled")]
        public bool CopyManagementEnabled { get; set; } = false;

        [BsonElement("copy_prefix")]
        public string CopyPrefix { get; set; } = "SDS"; // Prefix for copy IDs like "SDS-0232"

        [BsonElement("next_copy_number")]
        public int NextCopyNumber { get; set; } = 1;

        // 🔸 Computed properties (not stored in MongoDB)
        [BsonIgnore]
        public bool IsAvailable => AvailableCopies > 0;

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("is_reference_only")]
        public bool IsReferenceOnly { get; set; } = false;

        [BsonIgnore]
        public int BorrowedCopies => TotalCopies - AvailableCopies;

        [BsonIgnore]
        public string DisplayId => _id.ToString().Substring(0, 8) + "...";

        [BsonIgnore]
        public string NextCopyId => $"{CopyPrefix}-{NextCopyNumber:D4}";
    }
}
