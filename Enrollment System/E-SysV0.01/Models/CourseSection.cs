using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace E_SysV0._01.Models
{
    public class CourseSection
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty; // e.g., "BSIT-2025-Block-1"

        public string Program { get; set; } = string.Empty; // e.g., "BSIT"
        public string Name { get; set; } = string.Empty; // e.g., "Freshmen-Block-1"
        public int Capacity { get; set; } = 40;
        public int CurrentCount { get; set; } = 0;
        public int Year { get; set; }
    }
}