using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class BookCopy
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; } = "";

        [BsonElement("book_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string BookId { get; set; } = "";

        [BsonElement("copy_id")]
        public string CopyId { get; set; } = ""; // Human-readable ID like "SDS-0232"

        [BsonElement("barcode")]
        public string Barcode { get; set; } = "";

        [BsonElement("status")]
        public BookCopyStatus Status { get; set; } = BookCopyStatus.Available;

        [BsonElement("condition")]
        public CopyCondition Condition { get; set; } = CopyCondition.Good;

        [BsonElement("location")]
        public string Location { get; set; } = ""; // Shelf location

        [BsonElement("acquisition_date")]
        public DateTime AcquisitionDate { get; set; } = DateTime.UtcNow;

        [BsonElement("last_borrowed_date")]
        public DateTime? LastBorrowedDate { get; set; }

        [BsonElement("last_returned_date")]
        public DateTime? LastReturnedDate { get; set; }

        [BsonElement("borrow_count")]
        public int BorrowCount { get; set; } = 0;

        [BsonElement("notes")]
        public string? Notes { get; set; }

        [BsonElement("created_by")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string CreatedBy { get; set; } = "";

        [BsonElement("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [BsonElement("modified_by")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ModifiedBy { get; set; }

        [BsonElement("modified_date")]
        public DateTime? ModifiedDate { get; set; }

        // Navigation properties (not stored in DB)
        [BsonIgnore]
        public Book? Book { get; set; }

        [BsonIgnore]
        public User? CreatedByUser { get; set; }

        [BsonIgnore]
        public User? ModifiedByUser { get; set; }

        // Helper properties
        [BsonIgnore]
        public bool IsAvailable => Status == BookCopyStatus.Available;

        [BsonIgnore]
        public bool IsBorrowed => Status == BookCopyStatus.Borrowed;

        [BsonIgnore]
        public bool IsLost => Status == BookCopyStatus.Lost;

        [BsonIgnore]
        public bool IsDamaged => Status == BookCopyStatus.Damaged;

        [BsonIgnore]
        public bool NeedsRepair => Condition == CopyCondition.Damaged || Condition == CopyCondition.Poor;

        [BsonIgnore]
        public string StatusDisplayName => Status switch
        {
            BookCopyStatus.Available => "Available",
            BookCopyStatus.Borrowed => "Borrowed",
            BookCopyStatus.Lost => "Lost",
            BookCopyStatus.Damaged => "Damaged",
            BookCopyStatus.Maintenance => "Under Maintenance",
            BookCopyStatus.Retired => "Retired",
            _ => "Unknown"
        };

        [BsonIgnore]
        public string ConditionDisplayName => Condition switch
        {
            CopyCondition.Excellent => "Excellent",
            CopyCondition.Good => "Good",
            CopyCondition.Fair => "Fair",
            CopyCondition.Poor => "Poor",
            CopyCondition.Damaged => "Damaged",
            _ => "Unknown"
        };
    }
}
