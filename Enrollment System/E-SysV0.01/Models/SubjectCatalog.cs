using System;
using System.Collections.Generic;

namespace E_SysV0._01.Models
{
    public static class SubjectCatalog
    {
        // For now, enumerate what exists under Models\BSITSubjectModels\*
        // You can later reflect types or load from DB if needed.
        private static readonly HashSet<string> _bsitYearLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            "1st Year", "2nd Year", "3rd Year", "4th Year"
        };

        private static readonly HashSet<string> _bsitSemesters = new(StringComparer.OrdinalIgnoreCase)
        {
            "1st Semester", "2nd Semester"
        };

        // If BSENT subject models are added later, register them here.
        private static readonly HashSet<string> _bsentYearLevels = new(StringComparer.OrdinalIgnoreCase)
        {
            "1st Year", "2nd Year", "3rd Year", "4th Year"
        };

        private static readonly HashSet<string> _bsentSemesters = new(StringComparer.OrdinalIgnoreCase)
        {
            "1st Semester", "2nd Semester"
        };

        public static bool IsValidYearLevel(string programCode, string yearLevel)
        {
            var (years, _) = GetSets(programCode);
            return years.Contains(yearLevel);
        }

        public static bool IsValidSemester(string programCode, string semester)
        {
            var (_, sems) = GetSets(programCode);
            return sems.Contains(semester);
        }

        public static string GetDefaultYearLevel(string programCode)
        {
            // Default to 1st Year if available
            return IsValidYearLevel(programCode, "1st Year") ? "1st Year" : "1st Year";
        }

        public static string GetDefaultSemester(string programCode, string? preferredFromSettings)
        {
            if (!string.IsNullOrWhiteSpace(preferredFromSettings) &&
                IsValidSemester(programCode, preferredFromSettings))
            {
                return preferredFromSettings!;
            }
            return "1st Semester";
        }

        private static (HashSet<string> Years, HashSet<string> Sems) GetSets(string programCode)
        {
            if (programCode.Equals("BSENT", StringComparison.OrdinalIgnoreCase))
                return (_bsentYearLevels, _bsentSemesters);

            // Default to BSIT sets
            return (_bsitYearLevels, _bsitSemesters);
        }
    }
}