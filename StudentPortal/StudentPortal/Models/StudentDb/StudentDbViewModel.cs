using System.Collections.Generic;

namespace StudentPortal.Models.Studentdb
{
	public class AdminDashboardViewModel
	{
		public string UserName { get; set; } = "";
		public string Avatar { get; set; } = "";
		public string CurrentPage { get; set; } = "home";
		public List<ClassContent> Classes { get; set; } = new();
	}
}
