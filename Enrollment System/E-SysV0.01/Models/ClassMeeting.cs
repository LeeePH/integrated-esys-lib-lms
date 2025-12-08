using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace E_SysV0._01.Models
{
    // Discrete time-slot based meeting to enable unique indexes for conflict prevention.
    public class ClassMeeting
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty;

        public string SectionId { get; set; } = string.Empty;   // CourseSection.Id
        public string CourseCode { get; set; } = string.Empty;  // e.g., "MATH101"
        public string RoomId { get; set; } = string.Empty;      // Room.Id

        // 0=Sunday ... 6=Saturday (aligns with System.DayOfWeek)
        public int DayOfWeek { get; set; }

        // Discrete slot number (e.g., 1..8). Example: slot 1=08:00-09:30, 2=09:30-11:00, etc.
        public int Slot { get; set; }

        // Optional display-only friendly times if desired (not used for conflict checks)
        public string? DisplayTime { get; set; } // e.g., "08:00-09:30"
    }
}