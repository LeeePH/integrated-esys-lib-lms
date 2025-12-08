using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace ProfessorAccountCreation.Models
{
    public class ProfAdmin
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("email")]
        public string Email { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; }

        [BsonElement("otp")]
        public string OTP { get; set; }

        [BsonElement("otpExpiresAt")]
        public DateTime OTPExpiresAt { get; set; }
    }
}
