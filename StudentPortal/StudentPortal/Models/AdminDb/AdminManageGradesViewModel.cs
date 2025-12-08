using System;
using System.Collections.Generic;

namespace StudentPortal.Models.AdminDb
{
	public class AdminManageGradesViewModel
	{
		public string AdminName { get; set; } = "Admin";
		public string AdminInitials { get; set; } = "AD";
		public string ClassCode { get; set; } = "";
		public List<StudentGradeRecord> Students { get; set; } = new();
	}

	public class StudentGradeRecord
	{
		public string Id { get; set; } = "";
		public string Last { get; set; } = "";
		public string First { get; set; } = "";
		public string Middle { get; set; } = "";
		public string AvatarColor { get; set; } = "#6b89d4";

		public List<GradeTask> Tasks { get; set; } = new();
		public List<GradeAssessment> Assessments { get; set; } = new();
		public List<GradeAttendance> Attendance { get; set; } = new();
	}

	public class GradeTask
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public bool Submitted { get; set; }
		public double? Score { get; set; }
		public double Max { get; set; }
		public DateTime Due { get; set; }
	}

	public class GradeAssessment
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public bool Submitted { get; set; }
		public double? Score { get; set; }
		public double Max { get; set; }
		public DateTime Due { get; set; }
	}

	public class GradeAttendance
	{
		public DateTime Date { get; set; }
		public string Status { get; set; } = "Present"; // Present, Absent, Late
	}
}
