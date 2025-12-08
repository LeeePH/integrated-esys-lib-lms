namespace E_SysV0._01.Models
{
    public class StudentDashboardViewModel
    {
        public string? StudentId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Type { get; set; }
        public bool FirstLogin { get; set; }
        public EnrollmentRequest? Enrollment { get; set; }
        public string? SectionId { get; set; }
        public List<ClassMeeting> Schedule { get; set; }
        public bool EnrollmentOpen { get; set; }
        public string EnrollmentSemester { get; set; }
        public string EnrollmentAcademicYear { get; set; }

        // Subjects + schedule merged rows
        public List<StudentSubjectScheduleRow> SubjectSchedules { get; set; } = new();

        // Full history of all submissions for this account (by email)
        public List<EnrollmentRequest> EnrollmentHistory { get; set; } = new();

        // Previous semester subjects (e.g., 1st sem when currently on 2nd)
        public List<StudentSubjectRow> PreviousSubjects { get; set; } = new();
        public Dictionary<string, string> SubjectRemarks { get; set; } = new Dictionary<string, string>();

        public List<StudentSubjectScheduleRow> SecondSemesterSubjects { get; set; } = new List<StudentSubjectScheduleRow>();
        public Dictionary<string, string> SecondSemesterEligibility { get; set; } = new Dictionary<string, string>();
        public bool ShowSecondSemesterSubjects { get; set; }
        public bool HasFailedSubjects { get; set; }
        public Dictionary<string, string> PreviousSubjectRemarks { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ✅ NEW: Track missing subjects from curriculum
        public List<string> MissingSubjects { get; set; } = new();

        // ✅ NEW: Track failed subjects
        public List<string> FailedSubjects { get; set; } = new();
        public bool CanEnrollFor2ndYear { get; set; }

   
        public string? SecondYearEnrollmentType { get; set; }


        public string? TargetYearLevel { get; set; }

    
        public string? TargetSemester { get; set; }

   
        public Dictionary<string, string> CombinedFirstYearRemarks { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public List<SecondYearSubjectEligibility> SecondYearSubjects { get; set; } = new();

        // ✅ NEW: Library Penalties
        public List<Penalty> Penalties { get; set; } = new();
        public List<Penalty> PendingPenalties { get; set; } = new();
        public decimal TotalPendingPenalties { get; set; } = 0;
        public bool HasPendingPenalties { get; set; } = false;

    }

    public class StudentSubjectScheduleRow
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int Units { get; set; }
        public int DayOfWeek { get; set; }
        public string DisplayTime { get; set; } = "";
        public string RoomId { get; set; } = "";
        public string PreRequisite { get; set; } = ""; // ✅ ADD THIS LINE

    }


    public class StudentSubjectRow
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int Units { get; set; }
    }

    /// <summary>
    /// Represents a 2nd Year subject with its eligibility status
    /// </summary>
    public class SecondYearSubjectEligibility
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int Units { get; set; }
        public List<string> Prerequisites { get; set; } = new();
        public bool IsEligible { get; set; }
        public string EligibilityReason { get; set; } = "";
    }
}