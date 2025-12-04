using System;
using System.Collections.Generic;
using System.Linq;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// Business rules and validation logic for enrollment processes
    /// </summary>
    public static class EnrollmentRules
    {
        /// <summary>
        /// Check if a student is eligible to advance to the next year level
        /// (e.g., 1st Year 2nd Sem → 2nd Year 1st Sem)
        /// </summary>
        public static bool IsEligibleForNextYearEnrollment(EnrollmentRequest currentEnrollment, EnrollmentSettings settings)
        {
            if (currentEnrollment == null) return false;

            // Must be currently enrolled
            if (!currentEnrollment.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
                return false;

            // Get current semester from ExtraFields
            var semester = currentEnrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "");
            if (string.IsNullOrWhiteSpace(semester))
                return false;

            // Must have completed 2nd semester of current year
            if (!semester.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                return false;

            // Safety check: Semester must have ended (prevents premature enrollment)
            if (settings?.Semester2EndsAtUtc.HasValue == true &&
                DateTime.UtcNow < settings.Semester2EndsAtUtc.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determine if a student should be marked as Regular or Irregular based on subject remarks
        /// </summary>
        public static string DetermineRegularity(Dictionary<string, string> subjectRemarks)
        {
            if (subjectRemarks == null || subjectRemarks.Count == 0)
                return "Regular"; // Default to Regular if no remarks

            // Check for any failed subjects
            var hasFailedSubjects = subjectRemarks.Values
                .Any(remark => remark.Equals("fail", StringComparison.OrdinalIgnoreCase));

            return hasFailedSubjects ? "Irregular" : "Regular";
        }

        /// <summary>
        /// Check if enrollment should be blocked due to ongoing subjects
        /// </summary>
        public static bool HasOngoingSubjectsBlockingEnrollment(Dictionary<string, string> subjectRemarks)
        {
            if (subjectRemarks == null || subjectRemarks.Count == 0)
                return false;

            // Check if ANY subject is still ongoing
            return subjectRemarks.Values
                .Any(remark => remark.Equals("ongoing", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Parse year level from string (e.g., "1st Year" -> 1)
        /// </summary>
        public static int ParseYearLevel(string yearLevelStr)
        {
            if (string.IsNullOrWhiteSpace(yearLevelStr))
                return 1; // Default to 1st year

            // Extract number from strings like "1st Year", "2nd Year", etc.
            var match = System.Text.RegularExpressions.Regex.Match(yearLevelStr, @"(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int level))
            {
                return level;
            }

            return 1; // Fallback
        }

        /// <summary>
        /// Get the next year level string
        /// </summary>
        public static string GetNextYearLevel(string currentYearLevel)
        {
            var level = ParseYearLevel(currentYearLevel);
            var nextLevel = level + 1;

            if (nextLevel > 4) return "4th Year"; // Cap at 4th year

            return nextLevel switch
            {
                1 => "1st Year",
                2 => "2nd Year",
                3 => "3rd Year",
                4 => "4th Year",
                _ => "1st Year"
            };
        }

        /// <summary>
        /// Validate that all required subject remarks are set (no "ongoing" allowed for next year enrollment)
        /// </summary>
        public static (bool IsValid, List<string> OngoingSubjects) ValidateRemarksForNextYearEnrollment(
            Dictionary<string, string> subjectRemarks,
            List<SubjectRow> requiredSubjects)
        {
            var ongoingSubjects = new List<string>();

            if (subjectRemarks == null || requiredSubjects == null)
                return (false, ongoingSubjects);

            foreach (var subject in requiredSubjects)
            {
                if (!subjectRemarks.TryGetValue(subject.Code, out var remark) ||
                    string.IsNullOrWhiteSpace(remark))
                {
                    ongoingSubjects.Add(subject.Code);
                    continue;
                }

                if (remark.Equals("ongoing", StringComparison.OrdinalIgnoreCase))
                {
                    ongoingSubjects.Add(subject.Code);
                }
            }

            return (ongoingSubjects.Count == 0, ongoingSubjects);
        }
    }
}