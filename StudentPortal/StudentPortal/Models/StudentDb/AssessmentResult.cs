namespace StudentPortal.Models.StudentDb
{
    public class AssessmentResult
    {
        public string Id { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string ClassCode { get; set; } = string.Empty;
        public string ContentId { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentEmail { get; set; } = string.Empty;
        public System.DateTime? SubmittedAt { get; set; }
        public double? Score { get; set; }
        public double? MaxScore { get; set; }
        public string Status { get; set; } = "pending";
    }
}
