using System.Collections.Generic;
using StudentPortal.Models.AdminDb;

namespace StudentPortal.Models.ProfessorDb
{
    public class ProfessorDashboardViewModel
    {
        public string ProfessorName { get; set; } = "Professor";
        public string ProfessorInitials { get; set; } = "PR";
        public List<ClassItem> Classes { get; set; } = new();
    }
}


