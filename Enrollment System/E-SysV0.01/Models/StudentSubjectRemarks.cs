using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace E_SysV0._01.Models
{
    public class StudentSubjectRemarks
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString(); // ✅ Auto-generate ObjectId

        public string StudentUsername { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectTitle { get; set; } = string.Empty;
        public int Units { get; set; }

        // Pass or Fail
        public string Remark { get; set; } = string.Empty;

        public string SemesterTaken { get; set; } = string.Empty; // e.g., "1st Semester"
        public string YearLevelTaken { get; set; } = string.Empty; // e.g., "1st Year"
        public string Program { get; set; } = string.Empty; // BSIT or BSENT
        public string AcademicYear { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}