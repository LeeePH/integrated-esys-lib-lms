using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace ProfessorAccountCreation.Models
{
    public class ProfessorAssignment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("professorId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string ProfessorId { get; set; }

        [BsonElement("classMeetingIds")]
        [BsonRepresentation(BsonType.ObjectId)]
        public List<string> ClassMeetingIds { get; set; } = new List<string>(); // up to 7 IDs
    }
}
