using System;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public class TransfereeStudentPersonalDetails
    {
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string? Extension { get; set; } = "";
        public string Sex { get; set; } = "";
        public string ContactNumber { get; set; } = "";
        public string EmailAddress { get; set; } = "";
    }

    public class TransfereeGuardianPersonalDetails
    {
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string? Extension { get; set; } = "";
        public string Sex { get; set; } = "";          // added property

        public string ContactNumber { get; set; } = "";
        public string Relationship { get; set; } = "";
    }

    public class TransfereeAddressInfo
    {
        public string HouseStreet { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    public class TransfereeAcademicInfo
    {
        public string Program { get; set; } = "";
        public string YearLevel { get; set; } = "1st Year";
        public string Semester { get; set; } = "2nd Semester";
        public string AcademicYear { get; set; } = "";
    }

    public class TransfereePreviousSchoolDetails
    {
        public string LastSchoolAttended { get; set; } = "";
        public string CourseTaken { get; set; } = "";
        public string LastAcademicYearAttended { get; set; } = "";
    }

    public class TransfereeSchoolInfo
    {
        public string SchoolName { get; set; } = "";
        public string SchoolType { get; set; } = "";
        public string YearGraduated { get; set; } = "";
        public string Strand { get; set; } = ""; 
    }

    public class TransfereeSchoolAddressInfo
    {
        public string City { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    public class TransfereeDocumentStatus
    {
        public bool Form138 { get; set; }
        public bool GoodMoral { get; set; }
        public bool Diploma { get; set; }
        public bool MedicalCertificate { get; set; }
        public bool CertificateOfIndigency { get; set; }
        public bool GuidanceClearance { get; set; }      // new required document
        public bool TranscriptOfRecords { get; set; }    // new required document
        public bool BirthCertificate { get; set; }       // kept if needed
    }

    public class TransfereeEnrollmentViewModel
    {
        // Structured sections
        public TransfereeStudentPersonalDetails StudentPersonal { get; set; } = new();
        public TransfereeAddressInfo StudentAddress { get; set; } = new();
        public TransfereeSchoolInfo ElementarySchool { get; set; } = new();
        public TransfereeSchoolAddressInfo ElementaryAddress { get; set; } = new();
        public TransfereeSchoolInfo HighSchool { get; set; } = new();
        public TransfereeSchoolAddressInfo HighSchoolAddress { get; set; } = new();
        public TransfereeSchoolInfo SeniorHigh { get; set; } = new();
        public TransfereeSchoolAddressInfo SeniorHighAddress { get; set; } = new();
        public TransfereeGuardianPersonalDetails GuardianPersonal { get; set; } = new();
        public TransfereeAddressInfo GuardianAddress { get; set; } = new();
        public TransfereeAcademicInfo Academic { get; set; } = new();
        public TransfereePreviousSchoolDetails PreviousSchool { get; set; } = new();

        // Documents status (transferee must submit all required transfer docs)
        public TransfereeDocumentStatus Documents { get; set; } = new();

        // Extra carry-through dictionary (used by your multi-step flow)
        public Dictionary<string, string> ExtraFields { get; set; } = new();
    }
}