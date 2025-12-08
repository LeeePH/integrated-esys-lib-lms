using System.Collections.Generic;

namespace StudentPortal.Models.StudentDb
{
	public class StudentClassViewModel
	{
		public string SubjectName { get; set; } = string.Empty;
		public string SubjectCode { get; set; } = string.Empty;
		public string ClassCode { get; set; } = string.Empty;
		public string InstructorName { get; set; } = string.Empty;
		public string InstructorInitials { get; set; } = string.Empty;

		public string UserName { get; set; } = string.Empty;
		public string Avatar { get; set; } = string.Empty;

		public List<ContentCard> Contents { get; set; } = new List<ContentCard>();
	}

	public class ContentCard
	{
		public string Type { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string Meta { get; set; } = string.Empty;
		public string TargetAction { get; set; } = string.Empty;
		public string Urgency { get; set; } = string.Empty;
	}
}
