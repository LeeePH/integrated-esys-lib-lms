using System;
using System.Collections.Generic;

namespace StudentPortal.Models.StudentDb
{
	public class StudentAnswerAssessmentViewModel
	{
		public string StudentName { get; set; } = "";
		public string StudentInitials { get; set; } = "";

		public string SubjectName { get; set; } = "General Science";
		public string AssessmentTitle { get; set; } = "Assessment";
		public string FormUrl { get; set; } = "";
		public List<AssessmentQuestion> Questions { get; set; } = new();
	}

	public class AssessmentQuestion
	{
		public string Id { get; set; } = Guid.NewGuid().ToString();
		public QuestionType Type { get; set; } = QuestionType.ShortText;
		public string Text { get; set; } = "";
		public List<string> Options { get; set; } = new(); // for MCQ
	}

	public enum QuestionType
	{
		MultipleChoice,
		ShortText,
		LongText
	}

	// Submission model: used by both form posts and JSON posts
	public class StudentAnswerSubmission
	{
		public string StudentId { get; set; } = ""; // optional

		public List<QuestionAnswer> Answers { get; set; } = new();
		public string AntiCheatSummary { get; set; } = "";
		public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
	}

	public class QuestionAnswer
	{
		public string QuestionId { get; set; } = "";
		public string Response { get; set; } = ""; // selected option or text
	}

	// Anti-cheat log/report shape (optional)
	public class AntiCheatReport
	{
		public string Source { get; set; } = "client";
		public string Summary { get; set; } = "";
		public List<AntiCheatEvent> Events { get; set; } = new();
		public DateTime ReportedAtUtc { get; set; } = DateTime.UtcNow;
	}

	public class AntiCheatEvent
	{
		public string Type { get; set; } = "";
		public string Detail { get; set; } = "";
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
	}


	public class AntiCheatLogRequest
	{
		public string ResultId { get; set; } = "";
		public string EventType { get; set; } = "";
		public string Details { get; set; } = "";
		public int EventCount { get; set; }
		public int EventDuration { get; set; }
		public string Severity { get; set; } = "low";
		public bool Flagged { get; set; }
	}
}
