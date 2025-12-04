using System;
using System.Collections.Generic;
using System.Linq;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// Utility class for handling subject retake logic and eligibility calculations
    /// </summary>
    public static class SubjectRetakeHelper
    {
        /// <summary>
        /// Represents a subject that is available for retake
        /// </summary>
        public class RetakeOpportunity
        {
            public string Code { get; set; } = "";
            public string Title { get; set; } = "";
            public int Units { get; set; }
            public string PreRequisite { get; set; } = "";
            public string FailedInSemester { get; set; } = "";
            public string FailedInYearLevel { get; set; } = "";
            public bool IsEligible { get; set; }
            public string IneligibilityReason { get; set; } = "";
        }

        /// <summary>
        /// Gets all failed subjects AND missing curriculum subjects available for retake
        /// </summary>
        public static List<RetakeOpportunity> GetRetakeOpportunities(
            Dictionary<string, string> studentRemarks,
            string currentSemester,
            string studentYearLevel,
            string program,
            List<SubjectRow> allSubjects)
        {
            var opportunities = new List<RetakeOpportunity>();

            if (studentRemarks == null)
                studentRemarks = new Dictionary<string, string>();

            // ✅ NEW: Get canonical curriculum subjects the student SHOULD have taken by now
            var expectedSubjects = GetExpectedSubjectsByYearLevel(studentYearLevel, program);

            // ✅ Find subjects that were either:
            // 1. Failed (remark = "fail")
            // 2. Missing (not in studentRemarks but in expected curriculum)
            var failedOrMissingSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add explicitly failed subjects
            foreach (var remark in studentRemarks)
            {
                if (string.Equals(remark.Value, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    failedOrMissingSubjects.Add(remark.Key);
                }
            }

            // ✅ NEW: Add missing subjects from expected curriculum
            foreach (var expectedCode in expectedSubjects)
            {
                if (!studentRemarks.ContainsKey(expectedCode))
                {
                    failedOrMissingSubjects.Add(expectedCode);
                }
            }

            foreach (var subjectCode in failedOrMissingSubjects)
            {
                var subject = allSubjects.FirstOrDefault(s =>
                    string.Equals(s.Code, subjectCode, StringComparison.OrdinalIgnoreCase));

                if (subject == null)
                    continue;

                // Check if retakes are allowed for this subject
                if (!subject.AllowRetake)
                {
                    opportunities.Add(new RetakeOpportunity
                    {
                        Code = subject.Code,
                        Title = subject.Title,
                        Units = subject.Units,
                        PreRequisite = subject.PreRequisite,
                        IsEligible = false,
                        IneligibilityReason = "Retakes not permitted for this subject"
                    });
                    continue;
                }

                // Check semester availability
                if (!IsSubjectAvailableInSemester(subject, currentSemester))
                {
                    opportunities.Add(new RetakeOpportunity
                    {
                        Code = subject.Code,
                        Title = subject.Title,
                        Units = subject.Units,
                        PreRequisite = subject.PreRequisite,
                        IsEligible = false,
                        IneligibilityReason = $"Not offered in {currentSemester}"
                    });
                    continue;
                }

                // Check year level eligibility (retakes bypass strict year matching)
                if (!IsSubjectAvailableForYearLevel(subject, studentYearLevel, isRetake: true))
                {
                    opportunities.Add(new RetakeOpportunity
                    {
                        Code = subject.Code,
                        Title = subject.Title,
                        Units = subject.Units,
                        PreRequisite = subject.PreRequisite,
                        IsEligible = false,
                        IneligibilityReason = $"Not available for {studentYearLevel}"
                    });
                    continue;
                }

                // Check prerequisites (must have passed all prerequisites NOW)
                var prereqCheck = CheckPrerequisites(subject, studentRemarks);
                if (!prereqCheck.allPassed)
                {
                    opportunities.Add(new RetakeOpportunity
                    {
                        Code = subject.Code,
                        Title = subject.Title,
                        Units = subject.Units,
                        PreRequisite = subject.PreRequisite,
                        IsEligible = false,
                        IneligibilityReason = $"Missing prerequisites: {string.Join(", ", prereqCheck.missing)}"
                    });
                    continue;
                }

                // Subject is eligible for retake
                opportunities.Add(new RetakeOpportunity
                {
                    Code = subject.Code,
                    Title = subject.Title,
                    Units = subject.Units,
                    PreRequisite = subject.PreRequisite,
                    IsEligible = true,
                    IneligibilityReason = ""
                });
            }

            return opportunities;
        }


        /// <summary>
        /// ✅ NEW: Gets all subjects a student should have taken by their current year level
        /// </summary>
        private static HashSet<string> GetExpectedSubjectsByYearLevel(string yearLevel, string program)
        {
            var expectedSubjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var isBSIT = string.Equals(program, "BSIT", StringComparison.OrdinalIgnoreCase);
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            if (yearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase))
            {
                // Students in 2nd year should have completed all 1st year subjects
                if (isBSIT)
                {
                    foreach (var s in BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                }
                else if (isBSENT)
                {
                    foreach (var s in BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                }
            }
            else if (yearLevel.Contains("3rd Year", StringComparison.OrdinalIgnoreCase))
            {
                // Students in 3rd year should have completed 1st + 2nd year subjects
                if (isBSIT)
                {
                    foreach (var s in BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                }
                else if (isBSENT)
                {
                    foreach (var s in BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                        expectedSubjects.Add(s.Code);
                    foreach (var s in BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                        expectedSubjects.Add(s.Code);
                }
            }
            // Add more year levels as needed...

            return expectedSubjects;
        }

        /// <summary>
        /// Checks if a subject is available in the specified semester
        /// </summary>
        public static bool IsSubjectAvailableInSemester(SubjectRow subject, string semester)
        {
            if (string.IsNullOrWhiteSpace(subject.AvailableInSemesters))
                return false;

            var availableSemesters = subject.AvailableInSemesters
                .Split(',')
                .Select(s => s.Trim())
                .ToList();

            return availableSemesters.Any(s =>
                string.Equals(s, semester, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a subject is available for the specified year level
        /// </summary>
        /// <param name="isRetake">If true, bypasses strict year-level restrictions</param>
        public static bool IsSubjectAvailableForYearLevel(
            SubjectRow subject,
            string yearLevel,
            bool isRetake = false)
        {
            if (string.IsNullOrWhiteSpace(subject.AvailableForYearLevels))
                return false;

            // Retakes bypass year-level restrictions (students can retake subjects from previous years)
            if (isRetake)
                return true;

            var availableYears = subject.AvailableForYearLevels
                .Split(',')
                .Select(y => y.Trim())
                .ToList();

            return availableYears.Any(y =>
                string.Equals(y, yearLevel, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if all prerequisites for a subject have been passed
        /// </summary>
        /// <returns>Tuple: (allPassed, list of missing prerequisites)</returns>
        public static (bool allPassed, List<string> missing) CheckPrerequisites(
            SubjectRow subject,
            Dictionary<string, string> studentRemarks)
        {
            if (string.IsNullOrWhiteSpace(subject.PreRequisite))
                return (true, new List<string>());

            var prerequisites = subject.PreRequisite
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            var missing = new List<string>();

            foreach (var prereq in prerequisites)
            {
                // Check if prerequisite exists in remarks and is passed
                if (!studentRemarks.TryGetValue(prereq, out var status) ||
                    !string.Equals(status, "pass", StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add(prereq);
                }
            }

            return (missing.Count == 0, missing);
        }

        /// <summary>
        /// Calculates eligibility for all subjects including retakes, applying prerequisite blocking rules
        /// </summary>
        /// <param name="regularSubjects">Subjects from the current enrollment window</param>
        /// <param name="retakeOpportunities">Failed subjects available for retake</param>
        /// <param name="studentRemarks">Student's subject history</param>
        /// <returns>Dictionary mapping subject codes to eligibility reasons</returns>
        public static Dictionary<string, string> CalculateEligibilityWithRetakes(
            List<SubjectRow> regularSubjects,
            List<RetakeOpportunity> retakeOpportunities,
            Dictionary<string, string> studentRemarks)
        {
            var eligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Process regular subjects
            foreach (var subject in regularSubjects)
            {
                var prereqCheck = CheckPrerequisites(subject, studentRemarks);

                if (!prereqCheck.allPassed)
                {
                    eligibility[subject.Code] = $"Cannot enroll: Missing prerequisites ({string.Join(", ", prereqCheck.missing)})";
                }
                else
                {
                    eligibility[subject.Code] = "Can enroll";
                }
            }

            // Process retake opportunities
            foreach (var retake in retakeOpportunities)
            {
                if (!retake.IsEligible)
                {
                    eligibility[retake.Code] = $"Cannot retake: {retake.IneligibilityReason}";
                }
                else
                {
                    eligibility[retake.Code] = "Can enroll (Retake)";
                }
            }

            return eligibility;
        }

        /// <summary>
        /// Validates a student's subject selection, blocking dependent subjects if prerequisites are being retaken
        /// </summary>
        /// <param name="selectedSubjectCodes">Subject codes the student wants to enroll in</param>
        /// <param name="allSubjects">All available subjects</param>
        /// <param name="studentRemarks">Student's subject history</param>
        /// <returns>Tuple: (isValid, list of validation errors)</returns>
        public static (bool isValid, List<string> errors) ValidateSubjectSelection(
            List<string> selectedSubjectCodes,
            List<SubjectRow> allSubjects,
            Dictionary<string, string> studentRemarks)
        {
            var errors = new List<string>();

            if (!selectedSubjectCodes.Any())
            {
                errors.Add("No subjects selected");
                return (false, errors);
            }

            var selectedSubjects = allSubjects
                .Where(s => selectedSubjectCodes.Contains(s.Code, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Check for concurrent prerequisite conflicts (cannot enroll in subject + its failed prerequisite)
            foreach (var subject in selectedSubjects)
            {
                if (string.IsNullOrWhiteSpace(subject.PreRequisite))
                    continue;

                var prerequisites = subject.PreRequisite
                    .Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                foreach (var prereq in prerequisites)
                {
                    // Check if prerequisite was failed and is being retaken
                    if (studentRemarks.TryGetValue(prereq, out var status) &&
                        string.Equals(status, "fail", StringComparison.OrdinalIgnoreCase) &&
                        selectedSubjectCodes.Contains(prereq, StringComparer.OrdinalIgnoreCase))
                    {
                        errors.Add($"Cannot enroll in {subject.Code} while retaking prerequisite {prereq}. Complete {prereq} first.");
                    }
                }
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Gets all subjects from all years/semesters for a given program
        /// </summary>
        public static List<SubjectRow> GetAllProgramSubjects(string program)
        {
            var allSubjects = new List<SubjectRow>();

            var isBSIT = string.Equals(program, "BSIT", StringComparison.OrdinalIgnoreCase);
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            if (isBSIT)
            {
                // BSIT 1st Year
                allSubjects.AddRange(BSITSubjectModels._1stYear._1stYear1stSem.Subjects);
                allSubjects.AddRange(BSITSubjectModels._1stYear._1stYear2ndSem.Subjects);

                // BSIT 2nd Year
                allSubjects.AddRange(BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects);
                allSubjects.AddRange(BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects);

                // BSIT 3rd Year
                allSubjects.AddRange(BSITSubjectModels._3rdYear._3rdYear1stSem.Subjects);
                allSubjects.AddRange(BSITSubjectModels._3rdYear._3rdYear2ndSem.Subjects);

                // BSIT 4th Year
                allSubjects.AddRange(BSITSubjectModels._4thYear._4thYear1stSem.Subjects);
                allSubjects.AddRange(BSITSubjectModels._4thYear._4thYear2ndSem.Subjects);
            }
            else if (isBSENT)
            {
                // BSENT 1st Year
                allSubjects.AddRange(BSENTSubjectModels._1stYear._1stYear1stSem.Subjects);
                allSubjects.AddRange(BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects);

                // BSENT 2nd Year
                allSubjects.AddRange(BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects);
                allSubjects.AddRange(BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects);

                // BSENT 3rd Year
                allSubjects.AddRange(BSENTSubjectModels._3rdYear._3rdYear1stSem.Subjects);
                allSubjects.AddRange(BSENTSubjectModels._3rdYear._3rdYear2ndSem.Subjects);

               
            }

            return allSubjects;
        }

        /// <summary>
        /// Calculates total unit load including regular and retake subjects
        /// </summary>
        public static (int regularUnits, int retakeUnits, int totalUnits, bool exceedsLimit) CalculateUnitLoad(
            List<string> selectedSubjectCodes,
            List<SubjectRow> allSubjects,
            List<RetakeOpportunity> retakeOpportunities,
            int maxUnits = 24)
        {
            var regularUnits = 0;
            var retakeUnits = 0;

            var retakeCodes = retakeOpportunities.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var code in selectedSubjectCodes)
            {
                var subject = allSubjects.FirstOrDefault(s =>
                    string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));

                if (subject == null)
                    continue;

                if (retakeCodes.Contains(code))
                    retakeUnits += subject.Units;
                else
                    regularUnits += subject.Units;
            }

            var totalUnits = regularUnits + retakeUnits;
            var exceedsLimit = totalUnits > maxUnits;

            return (regularUnits, retakeUnits, totalUnits, exceedsLimit);
        }

        /// <summary>
        /// Parses student remarks from multiple semesters into a unified dictionary
        /// </summary>
        public static Dictionary<string, string> MergeSubjectRemarks(params Dictionary<string, string>[] remarkSets)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var remarkSet in remarkSets)
            {
                if (remarkSet == null)
                    continue;

                foreach (var kvp in remarkSet)
                {
                    // Latest remark takes precedence (e.g., "pass" overrides previous "fail")
                    merged[kvp.Key] = kvp.Value;
                }
            }

            return merged;
        }
    }
}