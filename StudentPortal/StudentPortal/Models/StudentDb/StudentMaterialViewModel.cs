namespace StudentPortal.Models.StudentDb
{
	public class StudentMaterialViewModel
	{
		public string MaterialId { get; set; } = string.Empty;
		public string SubjectName { get; set; } = string.Empty;
		public string SubjectCode { get; set; } = string.Empty;
		public string ClassCode { get; set; } = string.Empty;
		public string InstructorName { get; set; } = string.Empty;
		public string InstructorInitials { get; set; } = string.Empty;

		public string MaterialTitle { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string UploadedBy { get; set; } = string.Empty;
		public DateTime UploadDate { get; set; }

		public List<string> RecentMaterials { get; set; } = new();
		public List<MaterialFile> Files { get; set; } = new();

	}

	public class MaterialFile
    {
		public string FileName { get; set; }
		public string FileUrl { get; set; }
	}
}
