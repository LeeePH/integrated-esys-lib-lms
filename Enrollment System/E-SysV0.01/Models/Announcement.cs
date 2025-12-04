
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace E_SysV0._01.Models
{
    public class Announcement
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string? ImagePath { get; set; }
        public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
        public string PostedBy { get; set; } = "admin";
    }
}