using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class EnrollmentListViewModel
    {
        public string Title { get; set; } = "";
        public List<EnrollmentRequest> Items { get; set; } = new();
        public EnrollmentSettings? EnrollmentSettings { get; set; }
    }
}