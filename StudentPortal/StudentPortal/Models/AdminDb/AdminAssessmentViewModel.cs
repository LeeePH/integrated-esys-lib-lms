using System.Collections.Generic;

namespace SIA_IPT.Models.AdminAssessment
{
    public class AdminAssessmentViewModel
    {
        public string AssessmentId { get; set; } = string.Empty;

		public string AdminInitials { get; set; } = string.Empty;
		public string AdminName { get; set; } = string.Empty;

		public string SubjectName { get; set; } = string.Empty;
		public string SubjectCode { get; set; } = string.Empty;
		public string ClassCode { get; set; } = string.Empty;
		public string InstructorName { get; set; } = string.Empty;
		public string InstructorInitials { get; set; } = string.Empty;
		public List<string> RecentMaterials { get; set; } = new();
		public string AssessmentTitle { get; set; } = string.Empty;
		public string AssessmentDescription { get; set; } = string.Empty;
		public List<string> Attachments { get; set; } = new();
		public string PostedDate { get; set; } = string.Empty;
        public string Deadline { get; set; } = string.Empty;
        public List<StudentSubmission> Submissions { get; set; } = new();

        public int LogCopy { get; set; }
        public int LogPaste { get; set; }
        public int LogInspect { get; set; }
        public int LogTabSwitch { get; set; }
        public int LogOpenPrograms { get; set; }
        public int LogScreenShare { get; set; }
    }

	public class StudentSubmission
	{
		public string StudentName { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
	}
}
