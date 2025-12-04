using System.Collections.Generic;

namespace SIA_IPT.Models.AdminAssessmentList
{
    public class AdminAssessmentListViewModel
    {
        public List<AssessmentItem> Assessments { get; set; } = new();

		// Optional: You can add more fields for future use, for example:
		// public string SearchQuery { get; set; }
		// public int TotalAssessments => Assessments?.Count ?? 0;
    }

    public class AssessmentItem
    {
        public string Id { get; set; }
        public string Title { get; set; }

        // Optionally, you can store more metadata
        // public string Subject { get; set; }
        // public string CreatedBy { get; set; }
		// public DateTime DateCreated { get; set; }
	}
}
