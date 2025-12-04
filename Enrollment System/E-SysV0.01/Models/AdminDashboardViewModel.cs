using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class AdminDashboardViewModel
    {
        public List<EnrollmentRequest> Pending1stSem { get; set; } = new();
        public List<EnrollmentRequest> Pending2ndSem { get; set; } = new();
        public List<EnrollmentRequest> Enrolled { get; set; } = new();
        public List<EnrollmentRequest> EnrolledRegular { get; set; } = new();
        public List<EnrollmentRequest> OnHold { get; set; } = new();
        public List<EnrollmentRequest> Rejected { get; set; } = new();
        public EnrollmentSettings? EnrollmentSettings { get; set; }
        public Dictionary<string, long>? ProgramEnrolledCounts { get; set; } // FIX: long to match service
    }
}