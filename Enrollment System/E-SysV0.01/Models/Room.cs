using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class Room
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = string.Empty; // e.g., "R101"

        public string Name { get; set; } = string.Empty; // e.g., "Room 101"
        public int Capacity { get; set; } = 40;
        public string? Building { get; set; }

        // Optional slot-based availability. If empty/null => available for all day/slot.
        public List<RoomSlotAvailability> Availability { get; set; } = new();
    }

    public class RoomSlotAvailability
    {
        // 0=Sunday ... 6=Saturday
        public int DayOfWeek { get; set; }
        // Discrete slot number (e.g., 1..8)
        public int Slot { get; set; }
    }
}