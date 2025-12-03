using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProfessorAccountCreation.Models
{
    [BsonIgnoreExtraElements] // This will ignore unknown fields in MongoDB
    public class Professor
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("lastName")]
        [Required(ErrorMessage = "Last Name is required")]
        [RegularExpression(@"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.,-]+$", ErrorMessage = "Last Name contains invalid characters")]
        public string LastName { get; set; }

        [BsonElement("givenName")]
        [Required(ErrorMessage = "Given Name is required")]
        [RegularExpression(@"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.,-]+$", ErrorMessage = "Given Name contains invalid characters")]
        public string GivenName { get; set; }

        [BsonElement("middleName")]
        [RegularExpression(@"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "Middle Name contains invalid characters")]
        public string? MiddleName { get; set; }

        [BsonElement("extension")]
        [RegularExpression(@"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "Extension contains invalid characters")]
        public string? Extension { get; set; }

        [BsonElement("email")]
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address (e.g. name@example.com)")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
          ErrorMessage = "Please enter a valid email address (e.g. name@example.com)")]
        [StringLength(150, ErrorMessage = "Email cannot exceed 150 characters")]
        public string Email { get; set; }

        [BsonElement("programs")]
        [Required(ErrorMessage = "At least one program must be selected")]
        public List<string> Programs { get; set; } = new List<string>();

        [BsonElement("bachelor")]
        [RegularExpression(@"^[A-Za-z0-9À-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "Bachelor field contains invalid characters")]
        public string? Bachelor { get; set; }

        [BsonElement("masters")]
        [RegularExpression(@"^[A-Za-z0-9À-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "Masters field contains invalid characters")]
        public string? Masters { get; set; }

        [BsonElement("phd")]
        [RegularExpression(@"^[A-Za-z0-9À-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "PhD field contains invalid characters")]
        public string? PhD { get; set; }

        [BsonElement("licenses")]
        [RegularExpression(@"^[A-Za-z0-9À-ÖØ-öø-ÿ\s'.,-]*$", ErrorMessage = "Licenses field contains invalid characters")]
        public string? Licenses { get; set; }

        [BsonElement("facultyRole")]
        [Required(ErrorMessage = "Faculty Role is required")]
        public string FacultyRole { get; set; }

        [BsonElement("passwordHash")]
        public string? PasswordHash { get; set; }

        [BsonElement("isTemporaryPassword")]
        public bool IsTemporaryPassword { get; set; } = false;

        [BsonElement("tempPasswordExpiresAt")]
        public DateTime? TempPasswordExpiresAt { get; set; }
    }
}
