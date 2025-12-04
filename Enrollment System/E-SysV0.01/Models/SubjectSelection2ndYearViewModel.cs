using System;
using System.Collections.Generic;
using System.Linq;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// ViewModel for 2nd Year subject selection (irregular students)
    /// </summary>
    public class SubjectSelection2ndYearViewModel
    {
        public string StudentUsername { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;

        // ✅ NEW: Added missing properties used by the controller
        public string YearLevel { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public int MaxUnits { get; set; } = 24;

        /// <summary>
        /// All 1st Year subject remarks (both semesters)
        /// Key: SubjectCode, Value: Remark (pass/fail/ongoing)
        /// </summary>
        public Dictionary<string, string> FirstYearRemarks { get; set; } = new();

        /// <summary>
        /// Available 2nd Year 1st Semester subjects (with eligibility info)
        /// </summary>
        public List<SubjectRow> AvailableSubjects { get; set; } = new();

        /// <summary>
        /// ✅ NEW: Subjects with eligibility calculation (used by controller)
        /// </summary>
        public List<SecondYearSubjectEligibility> Subjects { get; set; } = new();

        /// <summary>
        /// Eligibility status for each 2nd Year subject
        /// Key: SubjectCode, Value: Eligibility reason
        /// </summary>
        public Dictionary<string, string> EligibilityStatus { get; set; } = new();

        /// <summary>
        /// Subject code to title mapping for display
        /// </summary>
        public Dictionary<string, string> SubjectTitles { get; set; } = new();

        /// <summary>
        /// Subject code to semester mapping (1st Semester / 2nd Semester)
        /// </summary>
        public Dictionary<string, string> SubjectSemesters { get; set; } = new();

        // Computed properties
        public int PassedSubjectsCount =>
            FirstYearRemarks?.Count(r => r.Value.Equals("pass", StringComparison.OrdinalIgnoreCase)) ?? 0;

        public int FailedSubjectsCount =>
            FirstYearRemarks?.Count(r => r.Value.Equals("fail", StringComparison.OrdinalIgnoreCase)) ?? 0;

        public int EligibleSubjectsCount =>
            Subjects?.Count(s => s.IsEligible) ?? 0;

        public int TotalUnits =>
            Subjects?.Sum(s => s.Units) ?? 0;

        public int EligibleUnits =>
            Subjects?.Where(s => s.IsEligible).Sum(s => s.Units) ?? 0;
    }
}