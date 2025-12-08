using System;
using System.Collections.Generic;
using System.Linq;
using E_SysV0._01.Models.BSITSubjectModels._1stYear;

namespace E_SysV0._01.Models
{
    // Helper methods for transferee eligibility & unit calculations.
    // Keeps logic out of views/controllers.
    public static class TransfereeEligibility
    {
        public static Dictionary<string, (string Title, int Units)> BuildSubjectLookup()
        {
            var dict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in _1stYear1stSem.Subjects)
                dict[s.Code] = (s.Title, s.Units);

            foreach (var s in _1stYear2ndSem.Subjects)
                dict[s.Code] = (s.Title, s.Units);

            return dict;
        }

        // Compute total units for subjects marked as "pass"
        public static int ComputePassedUnits(IEnumerable<string> passedCodes, IDictionary<string, (string Title, int Units)> lookup)
        {
            if (passedCodes == null) return 0;
            var set = new HashSet<string>(passedCodes.Where(c => !string.IsNullOrWhiteSpace(c)), StringComparer.OrdinalIgnoreCase);
            int sum = 0;
            foreach (var code in set)
            {
                if (lookup.TryGetValue(code, out var meta))
                    sum += meta.Units;
            }
            return sum;
        }

        // For each target second-sem subject determine eligibility
        // returns map subjectCode -> human-readable eligibility ("Can enroll" or "Missing prerequisites: X,Y")
        public static Dictionary<string, string> ComputeSecondSemesterEligibility(
            IEnumerable<string> passedSubjects,
            IDictionary<string, List<string>> prerequisites,
            IEnumerable<string> secondSemSubjects)
        {
            var passed = new HashSet<string>((passedSubjects ?? Array.Empty<string>()), StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subj in secondSemSubjects ?? Array.Empty<string>())
            {
                if (!prerequisites.TryGetValue(subj, out var reqs) || reqs == null || reqs.Count == 0)
                {
                    result[subj] = "Can enroll";
                    continue;
                }

                var missing = reqs.Where(r => !passed.Contains(r)).ToList();
                if (missing.Count == 0)
                    result[subj] = "Can enroll";
                else
                    result[subj] = "Missing prerequisites: " + string.Join(", ", missing);
            }

            return result;
        }
    }
}