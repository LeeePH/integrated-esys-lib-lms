using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string _id { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("user_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; set; }

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("user_role")]
        public string UserRole { get; set; }

        [BsonElement("action")]
        public string Action { get; set; }

        [BsonElement("action_category")]
        public string ActionCategory { get; set; } = ""; // BOOK_MANAGEMENT, USER_MANAGEMENT, BORROWING, SYSTEM, PENALTY, etc.

        [BsonElement("details")]
        public string Details { get; set; }

        [BsonElement("entity_type")]
        public string EntityType { get; set; }

        [BsonElement("entity_id")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EntityId { get; set; }

        [BsonElement("entity_name")]
        public string EntityName { get; set; } = ""; // Human-readable name like book title, user name

        [BsonElement("ip_address")]
        public string IpAddress { get; set; }

        [BsonElement("user_agent")]
        public string UserAgent { get; set; } = "";

        [BsonElement("session_id")]
        public string SessionId { get; set; } = "";

        [BsonElement("old_values")]
        public string OldValues { get; set; } = ""; // JSON string of previous values for updates

        [BsonElement("new_values")]
        public string NewValues { get; set; } = ""; // JSON string of new values for updates

        [BsonElement("success")]
        public bool Success { get; set; } = true;

        [BsonElement("error_message")]
        public string ErrorMessage { get; set; } = "";

        [BsonElement("duration_ms")]
        public long DurationMs { get; set; } = 0; // How long the action took

        // Helper properties for display
        [BsonIgnore]
        public string ActionIcon => ActionCategory switch
        {
            "BOOK_MANAGEMENT" => "📚",
            "USER_MANAGEMENT" => "👤",
            "BORROWING" => "📖",
            "SYSTEM" => "⚙️",
            "SECURITY" => "🔒",
            "REPORT" => "📊",
            "PENALTY" => "💰",
            _ => "📝"
        };

        [BsonIgnore]
        public string ActionColor => ActionCategory switch
        {
            "BOOK_MANAGEMENT" => "#2196F3",
            "USER_MANAGEMENT" => "#4CAF50",
            "BORROWING" => "#FF9800",
            "SYSTEM" => "#9C27B0",
            "SECURITY" => "#F44336",
            "REPORT" => "#00BCD4",
            "PENALTY" => "#FF5722",
            _ => "#607D8B"
        };

        [BsonIgnore]
        public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("MMM dd, yyyy h:mm:ss tt");

        [BsonIgnore]
        public string ShortTimestamp => Timestamp.ToLocalTime().ToString("M/d/yy h:mm tt");
    }
}