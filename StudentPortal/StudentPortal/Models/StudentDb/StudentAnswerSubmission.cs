using System.Collections.Generic;

namespace StudentPortal.Models.StudentDbLegacy
{
    public class StudentAnswerSubmission
    {
        public string? StudentId { get; set; }
        public string? AssessmentId { get; set; }
        public List<AnswerItem>? Answers { get; set; }
        public string? AntiCheatSummary { get; set; }
    }

    public class AnswerItem
    {
        public string QuestionId { get; set; } = string.Empty;
        public string? Response { get; set; }
    }
}