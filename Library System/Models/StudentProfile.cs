using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class StudentProfile
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("user_id")]
        public ObjectId UserId { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; }

        [BsonElement("contact_number")]
        public string ContactNumber { get; set; }

        [BsonElement("course")]
        public string Course { get; set; }

        [BsonElement("is_enrolled")]
        public bool IsEnrolled { get; set; }

        [BsonElement("is_flagged")]
        public bool IsFlagged { get; set; }

        [BsonElement("borrowing_limit")]
        public int BorrowingLimit { get; set; }

        [BsonElement("year_level")]
        public string YearLevel { get; set; }

        [BsonElement("program")]
        public string Program { get; set; }

        [BsonElement("department")]
        public string Department { get; set; }

        [BsonElement("total_penalties")]
        public decimal TotalPenalties { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; }

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; }
        [BsonElement("full_name")]
        public string FullName { get; set; }

    }
}
