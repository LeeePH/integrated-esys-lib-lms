using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class StudentMOCKData
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("student_number")]
        public string StudentNumber { get; set; }

        [BsonElement("full_name")]
        public string FullName { get; set; }

        [BsonElement("course")]
        public string Course { get; set; }

        [BsonElement("year_level")]
        public string YearLevel { get; set; }

        [BsonElement("program")]
        public string Program { get; set; }

        [BsonElement("department")]
        public string Department { get; set; }

        [BsonElement("contact_number")]
        public string ContactNumber { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("is_enrolled")]
        public bool IsEnrolled { get; set; }

        [BsonElement("enrollment_date")]
        public DateTime EnrollmentDate { get; set; }

        [BsonElement("graduation_date")]
        public DateTime? GraduationDate { get; set; }

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
