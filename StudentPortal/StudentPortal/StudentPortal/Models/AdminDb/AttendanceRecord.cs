using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace StudentPortal.Models.AdminDbLegacy
{
    public class AttendanceRecordLegacy
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string ClassId { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;

        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Present"; // Present, Absent, Late
    }
}