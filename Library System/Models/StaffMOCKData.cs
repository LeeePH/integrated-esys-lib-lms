using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SystemLibrary.Models
{
    public class StaffMOCKData
    {
        [BsonId]
        public ObjectId _id { get; set; }

        [BsonElement("employee_id")]
        public string EmployeeId { get; set; }

        [BsonElement("full_name")]
        public string FullName { get; set; }    

        [BsonElement("department")]
        public string Department { get; set; }

        [BsonElement("position")]
        public string Position { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("contact_number")]
        public string ContactNumber { get; set; }

        [BsonElement("employment_type")]
        public string EmploymentType { get; set; } // Full-time, Part-time, Contract, etc.

        [BsonElement("hire_date")]
        public DateTime HireDate { get; set; }

        [BsonElement("is_active")]
        public bool IsActive { get; set; } = true;

        [BsonElement("termination_date")]
        public DateTime? TerminationDate { get; set; }

        [BsonElement("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
