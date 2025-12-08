namespace StudentPortal.Models.AdminDb
{
	public class AttendanceStatus
	{
		public int Id { get; set; }
		public string StudentName { get; set; } = string.Empty;
		public string Status { get; set; } = "Present"; // Present, Absent, Late
		public string Date { get; set; } = string.Empty;
		public string Remarks { get; set; } = string.Empty;

		// Dummy seed data
		public static List<AttendanceStatus> GetDummyData() => new()
		{
			new AttendanceStatus { Id = 1, StudentName = "John Dela Cruz", Status = "Present", Date = "2025-11-01", Remarks = "On time" },
			new AttendanceStatus { Id = 2, StudentName = "Maria Santos", Status = "Late", Date = "2025-11-01", Remarks = "Arrived 10 mins late" },
			new AttendanceStatus { Id = 3, StudentName = "Carlo Ramirez", Status = "Absent", Date = "2025-11-01", Remarks = "Family emergency" },
			new AttendanceStatus { Id = 4, StudentName = "Ana Villanueva", Status = "Present", Date = "2025-11-01", Remarks = "Participated actively" }
		};
	}
}
