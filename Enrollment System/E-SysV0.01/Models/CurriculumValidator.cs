using System;
using System.Collections.Generic;
using System.Linq;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// Validates if a student has completed the full curriculum for their year level.
    /// Used to determine Regular vs Irregular status for next enrollment.
    /// </summary>
    public static class CurriculumValidator
    {
        /// <summary>
        /// Validates if student has taken ALL required subjects from the curriculum.
        /// </summary>
        /// <param name="program">Student's program (e.g., "BSIT", "BSENT")</param>
        /// <param name="completedYearLevel">Year level completed (e.g., "1st Year", "2nd Year")</param>
        /// <param name="completedSemester">Semester completed (e.g., "1st Semester", "2nd Semester")</param>
        /// <param name="studentRemarks">Dictionary of subject code -> remark (pass/fail/ongoing)</param>
        /// <returns>Tuple: (isRegular, missingSubjects, failedSubjects)</returns>
        public static (bool isRegular, List<string> missingSubjects, List<string> failedSubjects)
            ValidateCurriculumCompletion(
                string program,
                string completedYearLevel,
                string completedSemester,
                Dictionary<string, string> studentRemarks)
        {
            var missingSubjects = new List<string>();
            var failedSubjects = new List<string>();

            try
            {
                // Normalize program code
                var normalizedProgram = NormalizeProgramCode(program);
                var yearLevel = EnrollmentRules.ParseYearLevel(completedYearLevel);

                Console.WriteLine($"[CurriculumValidator] Validating {normalizedProgram} - {completedYearLevel} {completedSemester}");
                Console.WriteLine($"[CurriculumValidator] Student has {studentRemarks.Count} subject remarks");

                // Get all canonical subjects up to completed year/semester
                var canonicalSubjects = GetCanonicalSubjectsUpTo(normalizedProgram, yearLevel, completedSemester);

                Console.WriteLine($"[CurriculumValidator] Canonical curriculum has {canonicalSubjects.Count} required subjects");

                // Find missing subjects (subjects in curriculum but not in student's history)
                foreach (var subject in canonicalSubjects)
                {
                    if (!studentRemarks.ContainsKey(subject.Code))
                    {
                        missingSubjects.Add(subject.Code);
                        Console.WriteLine($"[CurriculumValidator] ❌ Missing: {subject.Code} ({subject.Title})");
                    }
                }

                // Find failed subjects (subjects student took but failed)
                foreach (var remark in studentRemarks)
                {
                    var normalized = (remark.Value ?? "").Trim().ToLowerInvariant();
                    if (normalized == "fail" || normalized == "failed" || normalized == "failing")
                    {
                        failedSubjects.Add(remark.Key);
                        Console.WriteLine($"[CurriculumValidator] ❌ Failed: {remark.Key} = {remark.Value}");
                    }
                }

                // Regular if: no missing subjects AND no failed subjects
                bool isRegular = !missingSubjects.Any() && !failedSubjects.Any();

                Console.WriteLine($"[CurriculumValidator] Result: {(isRegular ? "REGULAR" : "IRREGULAR")}");
                if (!isRegular)
                {
                    Console.WriteLine($"[CurriculumValidator] Reason: {missingSubjects.Count} missing, {failedSubjects.Count} failed");
                }

                return (isRegular, missingSubjects, failedSubjects);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CurriculumValidator] Error: {ex.Message}");
                // If error, assume irregular for safety
                return (false, missingSubjects, failedSubjects);
            }
        }

        /// <summary>
        /// Gets all canonical subjects up to and including the specified year/semester.
        /// </summary>
        private static List<SubjectRow> GetCanonicalSubjectsUpTo(
            string program,
            int yearLevel,
            string completedSemester)
        {
            var allSubjects = new List<SubjectRow>();
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            // Add all subjects from 1st year to completed year
            for (int year = 1; year <= yearLevel; year++)
            {
                // Add 1st semester subjects
                allSubjects.AddRange(GetSubjectsForYearAndSemester(program, year, true));

                // Add 2nd semester subjects only if:
                // - We're beyond this year, OR
                // - We're in this year AND completed 2nd semester
                bool include2ndSem = (year < yearLevel) ||
                                    (year == yearLevel && completedSemester.Contains("2nd", StringComparison.OrdinalIgnoreCase));

                if (include2ndSem)
                {
                    allSubjects.AddRange(GetSubjectsForYearAndSemester(program, year, false));
                }
            }

            return allSubjects;
        }

        /// <summary>
        /// Gets subjects for a specific year and semester.
        /// </summary>
        private static List<SubjectRow> GetSubjectsForYearAndSemester(
            string program,
            int year,
            bool is1stSem)
        {
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            return (year, is1stSem, isBSENT) switch
            {
                // BSIT
                (1, true, false) => BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                (1, false, false) => BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                (2, true, false) => BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                (2, false, false) => BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                (3, true, false) => BSITSubjectModels._3rdYear._3rdYear1stSem.Subjects.ToList(),
                (3, false, false) => BSITSubjectModels._3rdYear._3rdYear2ndSem.Subjects.ToList(),
                (4, true, false) => BSITSubjectModels._4thYear._4thYear1stSem.Subjects.ToList(),
                (4, false, false) => BSITSubjectModels._4thYear._4thYear2ndSem.Subjects.ToList(),

                // BSENT
                (1, true, true) => BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                (1, false, true) => BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                (2, true, true) => BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                (2, false, true) => BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                (3, true, true) => BSENTSubjectModels._3rdYear._3rdYear1stSem.Subjects.ToList(),
                (3, false, true) => BSENTSubjectModels._3rdYear._3rdYear2ndSem.Subjects.ToList(),

                _ => new List<SubjectRow>()
            };
        }

        /// <summary>
        /// Normalizes program code to standard format.
        /// </summary>
        private static string NormalizeProgramCode(string? program)
        {
            var p = (program ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(p)) return "BSIT";

            if (p.Contains("BSENT")) return "BSENT";
            if (p.Contains("BSIT")) return "BSIT";

            return "BSIT"; // Default
        }
    }
}