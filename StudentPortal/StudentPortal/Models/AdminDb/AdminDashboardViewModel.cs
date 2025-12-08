using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using StudentPortal.Utilities;

namespace StudentPortal.Models.AdminDb
{
    public class AdminDashboardViewModel
    {
        public string AdminName { get; set; } = "Admin";
        public string AdminInitials { get; set; } = "AD";
        public List<ClassItem> Classes { get; set; } = new();

    }

    public class ClassItem
    {
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString(); // ✅ Automatically generate a new ObjectId
        public string SubjectName { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string Section { get; set; } = "";
        public string Course { get; set; } = "";
        public string Year { get; set; } = "";
        public string Semester { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string BackgroundImageUrl { get; set; } = "";
        public string InstructorName { get; set; } = string.Empty;

        // Owner email used to filter classes per professor
        public string OwnerEmail { get; set; } = string.Empty;

        // optional: keep old properties for backwards compat/future code that used them
        public string CreatorName { get; set; } = string.Empty;
        public string CreatorInitials { get; set; } = string.Empty;
        public string CreatorRole { get; set; } = "Creator";

        [BsonIgnore]
        public string SectionLabel => SectionFormatter.Format(Course, Year, Section);

    }
}
