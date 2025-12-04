using System.Collections.Generic;

namespace StudentPortal.Models.StudentDb
{
	public class StudentAssessmentViewModel
	{
		public string AssessmentTitle { get; set; } = "";
		public string Description { get; set; } = "";
		public string PostedDate { get; set; } = "";
		public string Deadline { get; set; } = "";
		public string StudentName { get; set; } = "";
		public string StudentInitials { get; set; } = "";
		public List<TaskAttachment> Attachments { get; set; } = new();

		public string ClassCode { get; set; } = "";
		public string ContentId { get; set; } = "";
		public bool IsAnswered { get; set; } = false;
		public double? Score { get; set; }
		public double? MaxScore { get; set; }
	}
}
	
