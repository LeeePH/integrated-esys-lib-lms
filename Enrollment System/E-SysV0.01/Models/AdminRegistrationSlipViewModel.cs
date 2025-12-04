using System;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class AdminRegistrationSlipViewModel
    {
        // Header
        public string Program { get; set; } = "";
        public string YearLevel { get; set; } = "";
        public string Semester { get; set; } = "";
        public string SectionName { get; set; } = "";

        public string Regularity { get; set; } = "Regular";            // "Regular" | "Irregular"
        public string GraduatingStatus { get; set; } = "Not Graduating"; // "Graduating" | "Not Graduating"

        // Student
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string StudentNumber { get; set; } = "";
        public DateTime DateEnrolledUtc { get; set; }

        // Table
        public List<AdminRegistrationSlipSubject> Subjects { get; set; } = new();

        // Footer/signatures
        public string DeanName { get; set; } = "Dean Name";
        public DateTime RegistrationDateUtc { get; set; } = DateTime.UtcNow;

        // NEW: preserve the request id so views can open the server PDF endpoint
        public string RequestId { get; set; } = "";

        public DateTime DateEnrolledLocal => DateEnrolledUtc.ToLocalTime();
        public DateTime RegistrationDateLocal => RegistrationDateUtc.ToLocalTime();
    }

    public class AdminRegistrationSlipSubject
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int Units { get; set; }
        public string Room { get; set; } = "";
        public string Schedule { get; set; } = "";
        public bool IsRetake { get; set; }
    }
}