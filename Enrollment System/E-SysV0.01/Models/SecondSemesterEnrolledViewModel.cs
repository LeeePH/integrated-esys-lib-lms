using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// ViewModel for Admin - 2nd Semester Enrolled Details
    /// Used when admin reviews 1st Year 2nd Semester students to add remarks and calculate 2nd Year eligibility
    /// </summary>
    public class SecondSemesterEnrolledViewModel
    {
        public EnrollmentRequest Enrollment { get; set; } = new();
        public string StudentName { get; set; } = "";
        public string Program { get; set; } = "";
        public string Email { get; set; } = "";

        // 1st Semester subjects and remarks (read-only)
        public List<SubjectRow> FirstSemesterSubjects { get; set; } = new();
        public Dictionary<string, string> FirstSemesterRemarks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // 2nd Semester subjects and remarks (editable)
        public List<SubjectRow> SecondSemesterSubjects { get; set; } = new();
        public Dictionary<string, string> SecondSemesterRemarks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // 2nd Year 1st Semester eligibility (calculated)
        public List<SecondYearSubjectEligibility> SecondYearEligibility { get; set; } = new();
        public Dictionary<string, List<string>> PrerequisiteMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        // Current schedule (if any)
        public List<dynamic> CurrentSchedule { get; set; } = new();
        public bool HasCurrentSchedule { get; set; }
    }
}