namespace StudentPortal.Models.AdminDb
{
    public class CreateContentRequest
    {
        public string Type { get; set; } = ""; // material, task, assessment
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Deadline { get; set; } = "";
        public string Link { get; set; } = "";
        public string ClassId { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public long FileSize { get; set; }
        public int MaxGrade { get; set; } = 100;
    }
}
