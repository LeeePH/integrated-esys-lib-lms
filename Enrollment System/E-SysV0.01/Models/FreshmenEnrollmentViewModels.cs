namespace E_SysV0._01.Models
{
    public class StudentPersonalDetails
    {
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string? Extension { get; set; } = "";
        public string Sex { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string EmailAddress { get; set; } = "";
    }
    public class GuardianPersonalDetails
    {
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string? Extension { get; set; } = "";
        public string Sex { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string Relationship { get; set; } = "";
    }

    public class AddressInfo
    {
        public string HouseStreet { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    public class AcademicInfo
    {
        public string Program { get; set; } = "BSIT";
        public string YearLevel { get; set; } = "1st Year";
        public string Semester { get; set; } = "1st Semester";
        public string AcademicYear { get; set; } = "";
    }

    public class SchoolInfo
    {
        public string SchoolName { get; set; } = "";
        public string SchoolType { get; set; } = "";
        public string YearGraduated { get; set; } = "";
        public string Strand { get; set; } = "";
    }



    public class SchoolAddressInfo
    {
        public string City { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }



    public class FreshmenInfoModel
    {
        // Structured fields (Step 1)
        public StudentPersonalDetails StudentPersonal { get; set; } = new();
        public AddressInfo StudentAddress { get; set; } = new();
        public AcademicInfo Academic { get; set; } = new();
        public SchoolInfo ElementarySchool { get; set; } = new();
        public SchoolAddressInfo ElementaryAddress { get; set; } = new();
        public SchoolInfo HighSchool { get; set; } = new();
        public SchoolAddressInfo HighSchoolAddress { get; set; } = new();
        public SchoolInfo SeniorHigh { get; set; } = new();
        public SchoolAddressInfo SeniorHighAddress { get; set; } = new();
        public GuardianPersonalDetails GuardianPersonal { get; set; } = new();
        public AddressInfo GuardianAddress { get; set; } = new();

        // Legacy fields (kept for current steps)
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string EmergencyContactName { get; set; } = "";
        public string EmergencyContactPhone { get; set; } = "";
        public string Program { get; set; } = "";
    }

    public class SubjectRow
    {
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public int Units { get; set; }
        public string? Room { get; set; }
        public string? Schedule { get; set; }

        // Added: optional prerequisite description (keeps backward compatibility)
        public string PreRequisite { get; set; } = "";

        public string AvailableInSemesters { get; set; } = "1st Semester, 2nd Semester";
        public string AvailableForYearLevels { get; set; } = "1st Year, 2nd Year, 3rd Year, 4th Year";
        public bool AllowRetake { get; set; } = true;
    }

    public class FreshmenDocumentStatus
    {
        public bool Form138 { get; set; }
        public bool GoodMoral { get; set; }
        public bool Diploma { get; set; }
        public bool MedicalCertificate { get; set; }
        public bool CertificateOfIndigency { get; set; }
        public bool BirthCertificate { get; set; } // replaced CertificateOfResidency
    }

    public class FreshmenSubjectsViewModel
    {
        public FreshmenInfoModel Info { get; set; } = new();
        public List<SubjectRow> Subjects { get; set; } = new();
        public int TotalUnits { get; set; }
        public string YearLevel { get; set; } = "1st Year";
        public string Semester { get; set; } = "1st Semester";
        public FreshmenDocumentStatus Documents { get; set; } = new();
        public string TempUploadId { get; set; } = "";

        // Carry structured Step 1 fields through Step 2/3 to Confirm
        public Dictionary<string, string> ExtraFields { get; set; } = new();
    }
}