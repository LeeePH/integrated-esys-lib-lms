namespace StudentPortal.Models.AdminDb
{
    public class UpdateContentRequest
    {
        public string AssessmentId { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Deadline { get; set; }
    }

    public class DeleteContentRequest
    {
        public string AssessmentId { get; set; } = string.Empty;
    }
}
