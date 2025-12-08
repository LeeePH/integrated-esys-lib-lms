namespace StudentPortal.Models.StudentDb
{
    public class StudentAnnouncementViewModel
    {
        public string SubjectName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string InstructorInitials { get; set; } = string.Empty;

        public string Title { get; set; } = "Announcement";
        public string Description { get; set; } = string.Empty;
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public List<string> RecentAnnouncements { get; set; } = new();
    }
}
