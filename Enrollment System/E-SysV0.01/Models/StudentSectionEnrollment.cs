using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace E_SysV0._01.Models
{
    public class StudentSectionEnrollment
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty; // e.g., "{studentUsername}:{sectionId}"

        public string StudentUsername { get; set; } = string.Empty; // Student.Username
        public string SectionId { get; set; } = string.Empty;        // CourseSection.Id
        public DateTime EnrolledAt { get; set; }
    }
}