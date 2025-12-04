using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class ShifterEligibilityViewModel
    {
        public bool IsEligible { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Reasons { get; set; } = new();

        // Student basic info
        public string StudentUsername { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CurrentProgram { get; set; } = string.Empty;
        public string YearLevel { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;

        // Subject remarks
        public List<StudentSubjectRemarks> PassedSubjects { get; set; } = new();
        public List<StudentSubjectRemarks> FailedSubjects { get; set; } = new();

        public int TotalUnitsEarned { get; set; }
        public int TotalUnitsFailed { get; set; }
    }

    public class ShifterFormViewModel
    {
        // Personal Information (auto-filled)
        public string LastName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public string Sex { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;

        // Academic Information
        public string CourseLastEnrolled { get; set; } = string.Empty;
        public string CourseApplyingToShift { get; set; } = string.Empty;
        public int TotalUnitsEarned { get; set; }
        public int TotalUnitsFailed { get; set; }

        // Reason for Shifting
        public string ReasonForShifting { get; set; } = string.Empty;

        // Available program options (opposite of current)
        public List<string> AvailablePrograms { get; set; } = new();

        // Subject remarks for display
        public List<StudentSubjectRemarks> SubjectRemarks { get; set; } = new();
    }

    public class ShifterSubjectMatchViewModel
    {
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectTitle { get; set; } = string.Empty;
        public int Units { get; set; }
        public bool IsMatched { get; set; } // Exists in both programs
        public bool IsPassed { get; set; } // Student passed this subject
        public bool IsCredited { get; set; } // Will be credited (matched AND passed)
        public string Remark { get; set; } = string.Empty;
    }
}