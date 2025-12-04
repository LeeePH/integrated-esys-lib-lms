using E_SysV0._01.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace E_SysV0._01.Models
{
    /// <summary>
    /// Helper class for 2nd Year enrollment eligibility and subject filtering
    /// </summary>
    public static class SecondYearEnrollmentHelper
    {
        /// <summary>
        /// Determines if a student is eligible to enroll for 2nd Year 1st Semester
        /// </summary>
        public static bool IsEligibleFor2ndYear(EnrollmentRequest? enrollment, EnrollmentSettings? settings)
        {
            if (enrollment == null || settings == null) return false;

            // Must be currently enrolled
            if (!enrollment.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
                return false;

            // Must be 1st Year 2nd Semester
            var yearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");
            var semester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "");

            if (!yearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!semester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase))
                return false;

            // Enrollment period must be for 1st Semester
            if (!settings.Semester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase))
                return false;

            // Enrollment must be open
            if (!settings.IsOpen)
                return false;

            return true;
        }


        public static async Task<Dictionary<string, string>> ExtractAllFirstYearRemarksAsync(
     EnrollmentRequest enrollment,
     MongoDBServices db)
        {
            var remarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (enrollment == null)
            {
                Console.WriteLine("[ExtractAllFirstYearRemarksAsync] Enrollment is null");
                return remarks;
            }

            Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Starting extraction for {enrollment.Email}");

            // ✅ NEW: Check if this is a transferee
            var isTransferee = !string.IsNullOrWhiteSpace(enrollment.Type) &&
                              enrollment.Type.Contains("Transferee", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Is Transferee: {isTransferee}");

            // ✅ Step 1: Load from MongoDB (authoritative for regular students)
            try
            {
                var student = await db.GetStudentByEmailAsync(enrollment.Email);
                if (student != null)
                {
                    Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Found student: {student.Username}");

                    var mongoRemarks = await db.GetStudentSubjectRemarksAsync(student.Username);
                    if (mongoRemarks != null && mongoRemarks.Any())
                    {
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Found {mongoRemarks.Count} remarks in MongoDB");

                        foreach (var remark in mongoRemarks)
                        {
                            if (!string.IsNullOrWhiteSpace(remark.SubjectCode))
                            {
                                remarks[remark.SubjectCode] = remark.Remark ?? "ongoing";

                                // Determine semester
                                var semLabel = "Unknown";
                                if (remark.SemesterTaken != null)
                                {
                                    if (remark.SemesterTaken.Contains("1st", StringComparison.OrdinalIgnoreCase))
                                        semLabel = "1st Sem";
                                    else if (remark.SemesterTaken.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                                        semLabel = "2nd Sem";
                                }

                                Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] MongoDB {semLabel}: {remark.SubjectCode} = {remark.Remark}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[ExtractAllFirstYearRemarksAsync] No MongoDB remarks found");
                    }
                }
                else
                {
                    Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Student not found for email: {enrollment.Email}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] MongoDB error: {ex.Message}");
            }

            // ✅ Step 2: Merge from ExtraFields (supplements or overrides)
            if (enrollment.ExtraFields != null)
            {
                Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Processing {enrollment.ExtraFields.Count} ExtraFields");

                // Extract 1st semester remarks: "SubjectRemarks.CC101" = "pass"
                foreach (var kv in enrollment.ExtraFields.Where(x => x.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase)))
                {
                    // Skip 2nd semester remarks (handled below)
                    if (kv.Key.Contains(".2ndSem.", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var code = kv.Key.Replace("SubjectRemarks.", "", StringComparison.OrdinalIgnoreCase);

                    // ✅ CRITICAL: For transferees, ExtraFields is the PRIMARY source
                    if (isTransferee)
                    {
                        remarks[code] = kv.Value; // Always use (from TOR evaluation)
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Transferee ExtraFields: {code} = {kv.Value}");
                    }
                    else if (!remarks.ContainsKey(code))
                    {
                        remarks[code] = kv.Value;
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] ExtraFields 1st Sem: {code} = {kv.Value}");
                    }
                    else
                    {
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] ExtraFields 1st Sem: {code} already in MongoDB, skipping");
                    }
                }

                // ✅ Extract 2nd semester remarks: "SubjectRemarks.2ndSem.CC103" = "fail"
                foreach (var kv in enrollment.ExtraFields.Where(x => x.Key.Contains(".2ndSem.", StringComparison.OrdinalIgnoreCase)))
                {
                    var code = kv.Key.Replace("SubjectRemarks.2ndSem.", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("SubjectRemarks.", "", StringComparison.OrdinalIgnoreCase);

                    // 2nd semester always overrides (most recent data)
                    remarks[code] = kv.Value;
                    Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] ExtraFields 2nd Sem: {code} = {kv.Value}");
                }
            }
            else
            {
                Console.WriteLine("[ExtractAllFirstYearRemarksAsync] No ExtraFields found");
            }

            // ✅ Step 3: Check archives ONLY for regular students (transferees skip this)
            if (!isTransferee)
            {
                var academicYear = enrollment.ExtraFields?.GetValueOrDefault("Academic.AcademicYear", "");
                if (!string.IsNullOrWhiteSpace(academicYear))
                {
                    try
                    {
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Checking archives for AY: {academicYear}");

                        var archivedFirst = await db.GetArchivedFirstSemesterEnrollmentAsync(enrollment.Email, academicYear);
                        if (archivedFirst?.ExtraFields != null)
                        {
                            Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Found archived record with {archivedFirst.ExtraFields.Count} fields");

                            foreach (var kvp in archivedFirst.ExtraFields)
                            {
                                if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                                {
                                    var code = kvp.Key.Replace("SubjectRemarks.", "")
                                                     .Replace("2ndSem.", "");

                                    if (!remarks.ContainsKey(code))
                                    {
                                        remarks[code] = kvp.Value;
                                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Archive: {code} = {kvp.Value}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("[ExtractAllFirstYearRemarksAsync] No archived record found");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Archive error: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine("[ExtractAllFirstYearRemarksAsync] Skipping archive lookup for transferee");
            }

            Console.WriteLine($"[ExtractAllFirstYearRemarksAsync] Total extracted: {remarks.Count} remarks");
            return remarks;
        }

        /// <summary>
        /// Determines enrollment type (Regular/Irregular) based on failed subjects
        /// Wrapper method for 2nd Year specific terminology
        /// </summary>
        public static string DetermineSecondYearEnrollmentType(Dictionary<string, string> allRemarks)
        {
            if (allRemarks == null || allRemarks.Count == 0)
                return "2nd Year-Regular";

            // Check if any subject failed
            var hasFailedSubjects = allRemarks.Values
                .Any(remark => remark.Equals("fail", StringComparison.OrdinalIgnoreCase));

            return hasFailedSubjects ? "2nd Year-Irregular" : "2nd Year-Regular";
        }

        /// <summary>
        /// Filters 2nd Year 1st Sem subjects based on prerequisites from 1st Year
        /// </summary>
        public static List<SecondYearSubjectEligibility> Calculate2ndYearEligibility(
            List<SubjectRow> secondYearSubjects,
            Dictionary<string, string> allFirstYearRemarks,
            Dictionary<string, List<string>> prerequisiteMap)
        {
            var eligibilityList = new List<SecondYearSubjectEligibility>();

            Console.WriteLine($"[Calculate2ndYearEligibility] Checking {secondYearSubjects.Count} subjects with {allFirstYearRemarks.Count} remarks");

            foreach (var subject in secondYearSubjects)
            {
                var eligibility = new SecondYearSubjectEligibility
                {
                    Code = subject.Code,
                    Title = subject.Title,
                    Units = subject.Units,
                    Prerequisites = new List<string>()
                };

                // Get prerequisites for this subject
                if (prerequisiteMap.TryGetValue(subject.Code, out var prereqs))
                {
                    eligibility.Prerequisites = prereqs.ToList();
                }

                // Check eligibility
                if (eligibility.Prerequisites.Count == 0)
                {
                    // No prerequisites, always eligible
                    eligibility.IsEligible = true;
                    eligibility.EligibilityReason = "No prerequisites required";
                }
                else
                {
                    // Check if all prerequisites are passed
                    var failedPrereqs = new List<string>();
                    var ongoingPrereqs = new List<string>();

                    foreach (var prereq in eligibility.Prerequisites)
                    {
                        if (!allFirstYearRemarks.TryGetValue(prereq, out var remark))
                        {
                            Console.WriteLine($"[Calculate2ndYearEligibility] {subject.Code}: Prerequisite {prereq} has no remark");
                            ongoingPrereqs.Add(prereq);
                        }
                        else if (remark.Equals("fail", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Calculate2ndYearEligibility] {subject.Code}: Prerequisite {prereq} FAILED");
                            failedPrereqs.Add(prereq);
                        }
                        else if (remark.Equals("ongoing", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[Calculate2ndYearEligibility] {subject.Code}: Prerequisite {prereq} ONGOING");
                            ongoingPrereqs.Add(prereq);
                        }
                        else
                        {
                            Console.WriteLine($"[Calculate2ndYearEligibility] {subject.Code}: Prerequisite {prereq} PASSED");
                        }
                    }

                    if (failedPrereqs.Count > 0)
                    {
                        eligibility.IsEligible = false;
                        eligibility.EligibilityReason = $"Failed prerequisites: {string.Join(", ", failedPrereqs)}";
                    }
                    else if (ongoingPrereqs.Count > 0)
                    {
                        eligibility.IsEligible = false;
                        eligibility.EligibilityReason = $"Ongoing prerequisites: {string.Join(", ", ongoingPrereqs)}";
                    }
                    else
                    {
                        eligibility.IsEligible = true;
                        eligibility.EligibilityReason = "All prerequisites passed";
                    }
                }

                Console.WriteLine($"[Calculate2ndYearEligibility] {subject.Code}: IsEligible={eligibility.IsEligible}, Reason={eligibility.EligibilityReason}");
                eligibilityList.Add(eligibility);
            }

            return eligibilityList;
        }

        /// <summary>
        /// Gets prerequisite map for 2nd Year 1st Semester subjects
        /// </summary>
        /// <summary>
        /// Gets prerequisite map for 2nd Year 1st Semester subjects
        /// </summary>
        public static Dictionary<string, List<string>> GetSecondYearPrerequisites(string program)
        {
            var prereqMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(program, "BSIT", StringComparison.OrdinalIgnoreCase))
            {
                // ✅ BSIT 2nd Year 1st Sem prerequisites (from actual curriculum)
                prereqMap["CC104"] = new List<string> { "CC103" };           // Data Structures requires Intermediate Programming
                prereqMap["PE3"] = new List<string> { "PE1" };               // Individual/Dual Sports requires Rhythmic Activities
                prereqMap["CC105"] = new List<string> { "CC103" };           // Information Management requires Intermediate Programming
                prereqMap["NET102"] = new List<string> { "NET101", "PT101" }; // Networking 2 requires Networking 1 AND Platform Tech
                prereqMap["PF101"] = new List<string> { "CC103", "PT101" };  // OOP requires Intermediate Programming AND Platform Tech
                prereqMap["IS104"] = new List<string> { "CC103" };           // System Analysis requires Intermediate Programming
                                                                             // Subjects with NO prerequisites:
                                                                             // HUM1 - Art Appreciation (no prereq)
            }
            else if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
            {
                // ✅ BSENT 2nd Year 1st Sem prerequisites (from actual curriculum)
                prereqMap["ECC6"] = new List<string> { "EC2" };      // Business Law requires Financial Accounting 1
                prereqMap["ECC5"] = new List<string> { "EC2" };      // Financial Management requires Financial Accounting 1
                prereqMap["PE3"] = new List<string> { "PE1" };       // Individual/Dual Sports requires Rhythmic Activities
                prereqMap["ECC7"] = new List<string> { "EC1" };      // Market Research requires Entrepreneurial Marketing
                prereqMap["ECC8"] = new List<string> { "ECC1" };     // Opportunity Seeking requires Entrepreneurial Behavior
                                                                     // Subjects with NO prerequisites:
                                                                     // HUM1 - Art Appreciation (no prereq)
                                                                     // EC4 - Managing Service Enterprise (no prereq)
                                                                     // GEE1 - People and Earth's Ecosystems (no prereq)
                                                                     // RIZAL - Life and Works of Rizal (no prereq)
            }

            Console.WriteLine($"[GetSecondYearPrerequisites] Created prereq map for {program} with {prereqMap.Count} entries");
            foreach (var kvp in prereqMap)
            {
                Console.WriteLine($"  - {kvp.Key}: {string.Join(", ", kvp.Value)}");
            }

            return prereqMap;
        }
    }
}