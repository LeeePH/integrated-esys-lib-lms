// StudentPortal.Models.Studentdb.ClassContent.cs
namespace StudentPortal.Models.Studentdb
{
    public class ClassContent
    {
        public string Title { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string BackgroundImageUrl { get; set; } = string.Empty;

        // NEW: include class code so Student can navigate/join references
        public string ClassCode { get; set; } = string.Empty;
        // NEW: Approved / Pending
        public string Status { get; set; } = "";
    }
}
