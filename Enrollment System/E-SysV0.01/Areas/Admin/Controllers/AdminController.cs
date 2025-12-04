
using E_SysV0._01.Hubs;
using E_SysV0._01.Models;
using E_SysV0._01.Services;
using EnrollmentSystem.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace E_SysV0._01.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(AuthenticationSchemes = AdminScheme, Roles = AdminRole)]
    public class AdminController : Controller
    {
        private const string AdminScheme = "AdminCookie"; 
        private const string AdminRole = "Admin";

        // Freshmen required flags
        private static readonly string[] RequiredDocKeysFreshmen = new[] {
            "Form138", "GoodMoral", "Diploma", "MedicalCertificate", "CertificateOfIndigency", "BirthCertificate"
        };

        // Transferee required flags (per your spec)
        private static readonly string[] RequiredDocKeysTransferee = new[] {
            "Form138",
            "GoodMoral",
            "Diploma",
            "MedicalCertificate",
            "CertificateOfIndigency",
            "BirthCertificate",
            "GuidanceClearance",
            "TranscriptOfRecords"
        };

        private static readonly HashSet<string> AllowedFlagValues = new(StringComparer.OrdinalIgnoreCase) { "Submitted", "To be followed", "Ineligible" };

        private static readonly Dictionary<string, string> DocLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Form138"] = "Form 138",
            ["GoodMoral"] = "Good Moral",
            ["Diploma"] = "Diploma",
            ["MedicalCertificate"] = "Medical Certificate",
            ["CertificateOfIndigency"] = "Certificate of Indigency",
            ["BirthCertificate"] = "Birth Certificate",
            ["GuidanceClearance"] = "Guidance Clearance for Transfer",
            ["TranscriptOfRecords"] = "Transcript of Records"
        };


        private readonly MongoDBServices _db;
        private readonly EmailServices _email;
        private readonly IConfiguration _config;
        private readonly RegistrationSlipPdfService _pdf;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
               MongoDBServices db,
               EmailServices email,
               IConfiguration config,
               RegistrationSlipPdfService pdf,
               IHubContext<AdminNotificationsHub> hub,
               IWebHostEnvironment env,
               ILogger<AdminController> logger) // ✅ ADD THIS PARAMETER
        { 
            _db = db;
            _email = email;
            _config = config;
            _pdf = pdf;
            _hub = hub;
            _env = env;
            _logger = logger; // ✅ ADD THIS LINE

        }

        //Normalize cycle (auto-close enrollment, auto-start semester, move 1st->2nd, bump AY on 2nd end)
        private async Task NormalizeEnrollmentCycleAsync()
        {
            var settings = await _db.GetEnrollmentSettingsAsync();
            var now = DateTime.UtcNow;
            var changed = false;

            // Auto-close + auto-start semester at enrollment close
            if (settings.IsOpen && settings.ClosesAtUtc.HasValue && now >= settings.ClosesAtUtc.Value)
            {
                settings.IsOpen = false;
                changed = true;

                if (string.Equals(settings.Semester, "1st Semester", StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.Semester1StartedAtUtc == null)
                    {
                        var start = settings.ClosesAtUtc!.Value;
                        settings.Semester1StartedAtUtc = start;
                        var months = Math.Max(0, settings.Semester1PlannedMonths ?? 0);
                        var secs = Math.Max(0L, settings.Semester1PlannedDurationSeconds ?? 0L);
                        var end = start.AddMonths(months).AddSeconds(secs);
                        settings.Semester1EndsAtUtc = end;
                        changed = true;
                    }
                }
                else if (string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase))
                {
                    if (settings.Semester2StartedAtUtc == null)
                    {
                        var start = settings.ClosesAtUtc!.Value;
                        settings.Semester2StartedAtUtc = start;
                        var months = Math.Max(0, settings.Semester2PlannedMonths ?? 0);
                        var secs = Math.Max(0L, settings.Semester2PlannedDurationSeconds ?? 0L);
                        var end = start.AddMonths(months).AddSeconds(secs);
                        settings.Semester2EndsAtUtc = end;
                        changed = true;
                    }
                }
            }

            // 1st -> 2nd when 1st semester ends
            if (string.Equals(settings.Semester, "1st Semester", StringComparison.OrdinalIgnoreCase) &&
                settings.Semester1EndsAtUtc.HasValue && now >= settings.Semester1EndsAtUtc.Value)
            {
                settings.Semester = "2nd Semester";
                settings.IsOpen = false;
                settings.OpenedAtUtc = null;
                settings.ClosesAtUtc = null;
                settings.OpenDurationSeconds = null;
                changed = true;
            }

            // 2nd -> AY+1 and back to 1st when 2nd semester ends
            if (string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                settings.Semester2EndsAtUtc.HasValue && now >= settings.Semester2EndsAtUtc.Value)
            {
                settings.AcademicYear = IncrementAcademicYear(settings.AcademicYear);
                settings.Semester = "1st Semester";
                settings.IsOpen = false;
                settings.OpenedAtUtc = null;
                settings.ClosesAtUtc = null;
                settings.OpenDurationSeconds = null;

                // Clear runtime and planned to prepare for next cycle
                settings.Semester1StartedAtUtc = null;
                settings.Semester1EndsAtUtc = null;
                settings.Semester2StartedAtUtc = null;
                settings.Semester2EndsAtUtc = null;
                settings.Semester1PlannedMonths = null;
                settings.Semester1PlannedDurationSeconds = null;
                settings.Semester2PlannedMonths = null;
                settings.Semester2PlannedDurationSeconds = null;

                changed = true;
            }

            if (changed)
                await _db.UpsertEnrollmentSettingsAsync(settings);
        }

        private static string IncrementAcademicYear(string ay)
        {
            var t = (ay ?? "").Trim();
            if (t.Length >= 9 && int.TryParse(t.Substring(0, 4), out var y1))
            {
                var y2 = y1 + 1;
                return $"{y2}-{y2 + 1}";
            }
            var now = DateTime.UtcNow.Year;
            return $"{now}-{now + 1}";
        }
        private readonly IHubContext<AdminNotificationsHub> _hub; // ADD


        [HttpGet]
        public async Task<IActionResult> AdminSettings()
        {
            await NormalizeEnrollmentCycleAsync();

            var vm = new AdminDashboardViewModel
            {
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };

            var programs = ProgramCatalog.All.Select(p => p.Code);
            vm.ProgramEnrolledCounts = await _db.GetEnrolledCountsByProgramAsync(programs);

            ViewData["Title"] = "Admin Settings";
            return View(vm);
        }

        // Accept posted announcement (multipart form)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminPosting(string Title, string Content, IFormFile? AnnouncementImageFile)
        {
            Title = (Title ?? "").Trim();
            Content = (Content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Content))
            {
                TempData["Error"] = "Title and content are required.";
                return RedirectToAction(nameof(AdminPosting));
            }

            string? savedPath = null;
            if (AnnouncementImageFile != null && AnnouncementImageFile.Length > 0)
            {
                try
                {
                    var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "announcements");
                    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

                    var ext = Path.GetExtension(AnnouncementImageFile.FileName);
                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploads, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await AnnouncementImageFile.CopyToAsync(stream);
                    }

                    // relative path used for rendering
                    savedPath = $"/uploads/announcements/{fileName}";
                }
                catch
                {
                    // proceed without image
                    savedPath = null;
                }
            }

            var ann = new Announcement
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = Title,
                Content = Content,
                ImagePath = savedPath,
                PostedAtUtc = DateTime.UtcNow,
                PostedBy = User?.Identity?.Name ?? "admin"
            };

            try
            {
                await _db.InsertAnnouncementAsync(ann);
                TempData["Info"] = "Announcement posted.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to save announcement: " + ex.Message;
            }

            return RedirectToAction(nameof(AdminPosting));
        }

        // One-button configure for active semester (opens enrollment; plans semester duration)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetSemesterCycle(
                string semester,
                int enrollDays = 0, int enrollHours = 0, int enrollMinutes = 0, int enrollSeconds = 0,
                int semMonths = 0, int semDays = 0, int semHours = 0, int semMinutes = 0, int semSeconds = 0)
        {

            // ADD DEBUG OUTPUT
            Console.WriteLine($"Semester: {semester}");
            Console.WriteLine($"Enroll: {enrollDays}d {enrollHours}h {enrollMinutes}m {enrollSeconds}s");
            Console.WriteLine($"Semester: {semMonths}mo {semDays}d {semHours}h {semMinutes}m {semSeconds}s");


            await NormalizeEnrollmentCycleAsync();

            semester = (semester ?? "").Trim();
            if (semester != "1st Semester" && semester != "2nd Semester")
            {
                TempData["Error"] = "Invalid semester.";
                return RedirectToAction(nameof(AdminSettings));
            }

            var settings = await _db.GetEnrollmentSettingsAsync();
            if (!string.Equals(settings.Semester, semester, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Cannot configure inactive semester. Active: {settings.Semester}.";
                return RedirectToAction(nameof(AdminSettings));
            }

            // Guard: cannot modify after semester started
            if ((semester == "1st Semester" && settings.Semester1StartedAtUtc != null) ||
                (semester == "2nd Semester" && settings.Semester2StartedAtUtc != null))
            {
                TempData["Error"] = "Semester already started. Cannot change enrollment or semester duration.";
                return RedirectToAction(nameof(AdminSettings));
            }

            long enrollTotalSeconds =
                (long)Math.Max(0, enrollDays) * 86400L +
                (long)Math.Max(0, enrollHours) * 3600L +
                (long)Math.Max(0, enrollMinutes) * 60L +
                (long)Math.Max(0, enrollSeconds);

            if (enrollTotalSeconds <= 0)
            {
                TempData["Error"] = "Enrollment duration must be greater than zero.";
                return RedirectToAction(nameof(AdminSettings));
            }

            long semesterPlannedSeconds =
                (long)Math.Max(0, semDays) * 86400L +
                (long)Math.Max(0, semHours) * 3600L +
                (long)Math.Max(0, semMinutes) * 60L +
                (long)Math.Max(0, semSeconds);
            int semesterPlannedMonths = Math.Max(0, semMonths);

            if (semesterPlannedMonths == 0 && semesterPlannedSeconds <= 0)
            {
                TempData["Error"] = "Semester duration must be greater than zero.";
                return RedirectToAction(nameof(AdminSettings));
            }

            // Open enrollment now
            var now = DateTime.UtcNow;
            settings.Semester = semester;
            settings.IsOpen = true;
            settings.OpenedAtUtc = now;
            settings.OpenDurationSeconds = enrollTotalSeconds;
            settings.ClosesAtUtc = now.AddSeconds(enrollTotalSeconds);

            if (semester == "1st Semester")
            {
                settings.Semester1PlannedMonths = semesterPlannedMonths;
                settings.Semester1PlannedDurationSeconds = semesterPlannedSeconds;
            }
            else
            {
                settings.Semester2PlannedMonths = semesterPlannedMonths;
                settings.Semester2PlannedDurationSeconds = semesterPlannedSeconds;
            }

            await _db.UpsertEnrollmentSettingsAsync(settings);

            TempData["Info"] = $"{semester}: Enrollment opened and will auto-close at {settings.ClosesAtUtc:yyyy-MM-dd HH:mm} UTC. Semester will auto-start immediately after.";
            return RedirectToAction(nameof(AdminSettings));
        }

        // Reset cycle (stop everything and clear plan/runtime; keep AY as-is; set back to 1st)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetSemesterCycle()
        {
            var settings = await _db.GetEnrollmentSettingsAsync();

            settings.IsOpen = false;
            settings.OpenedAtUtc = null;
            settings.ClosesAtUtc = null;
            settings.OpenDurationSeconds = null;

            settings.Semester = "1st Semester";

            settings.Semester1PlannedMonths = null;
            settings.Semester1PlannedDurationSeconds = null;
            settings.Semester2PlannedMonths = null;
            settings.Semester2PlannedDurationSeconds = null;

            settings.Semester1StartedAtUtc = null;
            settings.Semester1EndsAtUtc = null;
            settings.Semester2StartedAtUtc = null;
            settings.Semester2EndsAtUtc = null;

            await _db.UpsertEnrollmentSettingsAsync(settings);
            TempData["Info"] = "Cycle has been reset. Configure the 1st Semester cycle to start again.";
            return RedirectToAction(nameof(AdminSettings));
        }



        // Helper: is all required flags set (any of submitted/to be followed/ineligible)
        private static bool AreFlagsComplete(EnrollmentRequest request)
        {
            if (request?.DocumentFlags == null) return false;

            var requiredKeys = GetRequiredDocKeysForRequest(request);

            foreach (var key in requiredKeys)
            {
                if (!request.DocumentFlags.TryGetValue(key, out var val)) return false;
                var normalized = (val ?? "").Trim();
                if (string.IsNullOrWhiteSpace(normalized)) return false;
                if (!AllowedFlagValues.Contains(normalized)) return false;
            }
            return true;
        }

        private static void MergePostedFlags(EnrollmentRequest request, Dictionary<string, string>? flags)
        {
            if (request == null || flags == null || flags.Count == 0) return;

            request.DocumentFlags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var requiredKeys = GetRequiredDocKeysForRequest(request);

            foreach (var kv in flags)
            {
                var key = (kv.Key ?? "").Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var isRequiredKey = false;
                foreach (var reqKey in requiredKeys)
                {
                    if (reqKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        key = reqKey;
                        isRequiredKey = true;
                        break;
                    }
                }
                if (!isRequiredKey) continue;

                var val = (kv.Value ?? "").Trim();
                if (!AllowedFlagValues.Contains(val)) continue;

                request.DocumentFlags[key] = char.ToUpper(val[0]) + val.Substring(1).ToLower();
            }
        }

        private static string BuildFlagsSummary(EnrollmentRequest request)
        {
            var lines = new List<string>();
            var requiredKeys = GetRequiredDocKeysForRequest(request);
            foreach (var key in requiredKeys)
            {
                var label = DocLabels.TryGetValue(key, out var lbl) ? lbl : key;
                var status = (request.DocumentFlags != null &&
                              request.DocumentFlags.TryGetValue(key, out var v) &&
                              !string.IsNullOrWhiteSpace(v))
                              ? v.Trim()
                              : "unset";
                lines.Add($"- {label}: {status}");
            }
            return string.Join("\n", lines);
        }

        private static bool IsSecondSemRequest(EnrollmentRequest request)
        {
            if (request == null) return false;
            if (string.Equals(request.Status, "2nd Sem Pending", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(request.Type, "Freshmen-Regular", StringComparison.OrdinalIgnoreCase)) return true;
            if (request.ExtraFields != null &&
                request.ExtraFields.TryGetValue("Academic.Semester", out var sem) &&
                sem.Trim().StartsWith("2", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static string[] GetRequiredDocKeysForRequest(EnrollmentRequest? request)
        {
            if (request == null) return RequiredDocKeysFreshmen;
            if (!string.IsNullOrWhiteSpace(request.Type) && request.Type.Equals("Transferee", StringComparison.OrdinalIgnoreCase))
                return RequiredDocKeysTransferee;

            // fallback: if extra field indicates transferee type (defensive)
            if (request.ExtraFields != null &&
                request.ExtraFields.TryGetValue("Applicant.Type", out var t) &&
                !string.IsNullOrWhiteSpace(t) &&
                t.Equals("Transferee", StringComparison.OrdinalIgnoreCase))
            {
                return RequiredDocKeysTransferee;
            }

            return RequiredDocKeysFreshmen;
        }

        private async Task<bool> EnrollRequestSecondSemAsync(EnrollmentRequest request)
        {

            try
            {
                var student = await _db.GetStudentByEmailAsync(request.Email);
                bool createdStudent = false;
                string? createdTempPassword = null;

                if (student == null)
                {
                    var year = DateTime.UtcNow.Year;
                    var tempUsername = await _db.GenerateNextStudentUsernameAsync(year);
                    var tempPassword = Guid.NewGuid().ToString("N")[..8];
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                    student = new E_SysV0._01.Models.Student
                    {
                        Username = tempUsername,
                        PasswordHash = passwordHash,
                        FirstLogin = true,
                        Email = (request.Email ?? string.Empty).Trim().ToLowerInvariant(),
                        Type = request.Type
                    };

                    await _db.CreateStudentAsync(student);
                    createdStudent = true;
                    createdTempPassword = tempPassword;
                }

                Console.WriteLine($"[EnrollRequestSecondSemAsync] 🔍 Archiving 1st semester enrolled records for {request.Email}");

                try
                {
                    var allRequests = await _db.GetRequestsByEmailAsync(request.Email);

                    if (allRequests == null || !allRequests.Any())
                    {
                        Console.WriteLine($"[EnrollRequestSecondSemAsync] ❌ No existing records found");
                        if (createdStudent) await _db.DeleteStudentByUsernameAsync(student.Username);
                        return false;
                    }

                    int archivedCount = 0;

                    foreach (var oldRequest in allRequests)
                    {
                        if (oldRequest.Id == request.Id) continue;

                        var isEnrolled = oldRequest.Status != null &&
                                         oldRequest.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase);

                        if (!isEnrolled) continue;

                        var oldSem = oldRequest.ExtraFields?.GetValueOrDefault("Academic.Semester", "");
                        var oldYear = oldRequest.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");

                        // ✅ Archive ONLY 1st Year 1st Semester
                        if (oldYear.Contains("1st Year", StringComparison.OrdinalIgnoreCase) &&
                            oldSem.Contains("1st", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[EnrollRequestSecondSemAsync] ✅ Archiving {oldRequest.Id}");

                            await _db.ArchiveEnrollmentRequestAsync(oldRequest, "Advanced to 2nd Semester");
                            await _db.DeleteEnrollmentRequestAsync(oldRequest.Id);

                            archivedCount++;
                        }
                    }

                    if (archivedCount == 0)
                    {
                        Console.WriteLine($"[EnrollRequestSecondSemAsync] ❌ No 1st semester record found!");
                        if (createdStudent) await _db.DeleteStudentByUsernameAsync(student.Username);
                        return false;
                    }

                    Console.WriteLine($"[EnrollRequestSecondSemAsync] ✅ Archived {archivedCount} record(s)");
                }
                catch (Exception archiveEx)
                {
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] ❌ Archiving failed: {archiveEx.Message}");
                    if (createdStudent) await _db.DeleteStudentByUsernameAsync(student.Username);
                    return false;
                }


                // Continue with enrollment...
                var program = GetProgramForRequest(request);
                var settings = await _db.GetEnrollmentSettingsAsync();

                // ✅ FIX: Properly prioritize EnrolledSubjects (prevents duplicates)
                var eligibleSubjects = new List<string>();

                // Get subjects from student's selection (includes retakes)
                if (request.ExtraFields != null && request.ExtraFields.TryGetValue("EnrolledSubjects", out var enrolledCsv) && !string.IsNullOrWhiteSpace(enrolledCsv))
                {
                    // Use the subjects the student actually selected
                    eligibleSubjects.AddRange(enrolledCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] Using student-selected subjects: {string.Join(", ", eligibleSubjects)}");
                }
                else if (request.SecondSemesterEligibility != null && request.SecondSemesterEligibility.Any())
                {
                    // Fallback: Use eligibility map
                    foreach (var kvp in request.SecondSemesterEligibility)
                    {
                        if (kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        {
                            eligibleSubjects.Add(kvp.Key);
                        }
                    }
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] Using eligibility map subjects: {string.Join(", ", eligibleSubjects)}");
                }
                else
                {
                    // Fallback: Assign all 2nd semester subjects
                    if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        eligibleSubjects.AddRange(
                            E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.Select(s => s.Code)
                        );
                    }
                    else
                    {
                        eligibleSubjects.AddRange(
                            E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.Select(s => s.Code)
                        );
                    }
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] Using canonical subjects: {string.Join(", ", eligibleSubjects)}");
                }

                // ✅ CRITICAL: Remove duplicates before creating schedule
                eligibleSubjects = eligibleSubjects.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Console.WriteLine($"[EnrollRequestSecondSemAsync] ✅ Final subject list (after deduplication): {string.Join(", ", eligibleSubjects)}");

                await _db.ClearStudentScheduleAsync(student.Username);

                var sectionName = $"Custom-{student.Username}-2ndSem";
                var customSection = new CourseSection
                {
                    Id = $"{program}-{DateTime.UtcNow.Year}-{student.Username}-2ndSem",
                    Program = program,
                    Name = sectionName,
                    Capacity = 1,
                    CurrentCount = 0,
                    Year = DateTime.UtcNow.Year
                };

                await _db.CreateCustomSectionAsync(customSection);
                await _db.GenerateSectionScheduleAsync(customSection.Id, eligibleSubjects);
                await _db.EnrollStudentInSectionAsync(student.Username, customSection.Id);

                var studentSchedule = await _db.GetStudentScheduleAsync(student.Username);
                if (studentSchedule == null || studentSchedule.Count == 0)
                {
                    await _db.DeleteSectionAsync(customSection.Id);
                    if (createdStudent) await _db.DeleteStudentByUsernameAsync(student.Username);
                    return false;
                }

                byte[] pdfBytes = await _pdf.GenerateForRequestAsync(request.Id);

                try
                {
                    if (createdStudent && createdTempPassword != null)
                    {
                        await _email.SendAcceptanceEmailAsync(request.Email, student.Username, createdTempPassword, pdfBytes);
                    }
                    else
                    {
                        await _email.SendSecondSemesterAcceptanceEmailAsync(request.Email, pdfBytes);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] Email failed: {ex.Message}");
                }

                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                request.ExtraFields["EnrolledSubjects"] = string.Join(",", eligibleSubjects);
                request.ExtraFields["Academic.Semester"] = "2nd Semester";

                bool isIrregular = request.Type != null && request.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase);

                request.Status = isIrregular ? "Enrolled - Irregular" : "Enrolled - Regular";
                request.LastUpdatedAt = DateTime.UtcNow;
                await _db.UpdateEnrollmentRequestAsync(request);

                // IMPORTANT: keep student.Type as Freshmen when advancing to 2nd SEM of 1st Year.
                try
                {
                    // Determine whether this enrollment represents 1st-Year 2nd-Sem (not advancing to 2nd Year)
                    var yearLevel = request.ExtraFields.TryGetValue("Academic.YearLevel", out var yl) ? (yl ?? "") : "";
                    var sem = request.ExtraFields.TryGetValue("Academic.Semester", out var s) ? (s ?? "") : "";

                    var advancedToSecondSemOfSameYear = yearLevel.Contains("1st", StringComparison.OrdinalIgnoreCase) &&
                                                       sem.Contains("2nd", StringComparison.OrdinalIgnoreCase);

                    if (advancedToSecondSemOfSameYear)
                    {
                        var newType = isIrregular ? "Freshmen-Irregular" : "Freshmen-Regular";
                        if (!string.Equals(student.Type ?? "", newType, StringComparison.OrdinalIgnoreCase))
                        {
                            student.Type = newType;
                            await _db.UpdateStudentAsync(student);
                            Console.WriteLine($"[EnrollRequestSecondSemAsync] Updated student.Type = {newType} for {student.Username}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequestSecondSemAsync] Warning: failed to update student.Type: {ex.Message}");
                }

                Console.WriteLine($"[EnrollRequestSecondSemAsync] ✅ Successfully completed 2nd semester enrollment");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnrollRequestSecondSemAsync] ❌ Error: {ex.Message}");
                return false;
            }

        }


        [HttpGet]
        public async Task<IActionResult> Enrolled2ndSem(string? q = null, string? program = null)
        {
            List<EnrollmentRequest> allEnrolled;

            if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
            {
                // ✅ Fetch ALL enrolled records
                var enrolled = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled") ?? new List<EnrollmentRequest>();
                var regular = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Regular") ?? new List<EnrollmentRequest>();
                var irregular = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Irregular") ?? new List<EnrollmentRequest>();

                allEnrolled = enrolled.Concat(regular).Concat(irregular).ToList();
            }
            else
            {
                var enrolled = await _db.SearchRequestsByStatusAsync("Enrolled", q, program) ?? new List<EnrollmentRequest>();
                var regular = await _db.SearchRequestsByStatusAsync("Enrolled - Regular", q, program) ?? new List<EnrollmentRequest>();
                var irregular = await _db.SearchRequestsByStatusAsync("Enrolled - Irregular", q, program) ?? new List<EnrollmentRequest>();

                allEnrolled = enrolled.Concat(regular).Concat(irregular).ToList();
            }

            // ✅ CRITICAL FIX: Filter to show ALL 2nd Semester Records (1st Year AND 2nd Year)
            var secondSemesterOnly = allEnrolled
                .Where(r => r.ExtraFields != null &&
                            r.ExtraFields.TryGetValue("Academic.Semester", out var sem) &&
                            sem.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.Email) // ✅ Remove duplicates by email
                .Select(g => g.OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt).First())
                .OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt)
                .ToList();

            var vm = new EnrollmentListViewModel
            {
                Title = "Enrolled Students (2nd Semester - All Years)",
                Items = secondSemesterOnly
            };

            Console.WriteLine($"[Enrolled2ndSem] Showing {secondSemesterOnly.Count} 2nd semester students (all years)");

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Enrolled2ndSemesterDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid enrollment ID.";
                return RedirectToAction(nameof(Enrolled2ndSem));
            }

            var enrollment = await _db.GetEnrollmentRequestByIdAsync(id);
            if (enrollment == null)
            {
                TempData["Error"] = "Enrollment record not found.";
                return RedirectToAction(nameof(Enrolled2ndSem));
            }

            var program = NormalizeProgramCode(enrollment.Program);
            var ef = enrollment.ExtraFields ?? new Dictionary<string, string>();

            // ✅ CRITICAL FIX: Load 1st semester remarks from BOTH MongoDB AND ExtraFields
            var student = await _db.GetStudentByEmailAsync(enrollment.Email);
            var firstSemRemarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Step 1: Try MongoDB first (authoritative source)
            if (student != null && !string.IsNullOrWhiteSpace(student.Username))
            {
                var mongoRemarks = await _db.GetStudentSubjectRemarksAsync(student.Username); // ✅ FIXED: Use student.Username
                if (mongoRemarks != null && mongoRemarks.Any())
                {
                    foreach (var remark in mongoRemarks)
                    {
                        // Filter for 1st semester subjects only
                        if (!string.IsNullOrWhiteSpace(remark.SubjectCode) &&
                            remark.SemesterTaken != null &&
                            remark.SemesterTaken.Contains("1st", StringComparison.OrdinalIgnoreCase))
                        {
                            firstSemRemarks[remark.SubjectCode] = remark.Remark ?? "ongoing";
                        }
                    }

                    Console.WriteLine($"[Enrolled2ndSemesterDetails] Loaded {firstSemRemarks.Count} MongoDB remarks for {student.Username}"); // ✅ FIXED
                }
            }

            // ✅ Step 2: Fallback to ExtraFields if MongoDB is empty
            if (!firstSemRemarks.Any())
            {
                foreach (var kv in ef.Where(x => x.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase) &&
                                                  !x.Key.Contains(".2ndSem.", StringComparison.OrdinalIgnoreCase)))
                {
                    var code = kv.Key.Replace("SubjectRemarks.", "");
                    firstSemRemarks[code] = kv.Value;
                }

                Console.WriteLine($"[Enrolled2ndSemesterDetails] Loaded {firstSemRemarks.Count} ExtraFields remarks (fallback)");
            }

            // ✅ Step 3: ALSO check archived records
            var academicYear = ef.GetValueOrDefault("Academic.AcademicYear", "");
            try
            {
                var archivedFirst = await _db.GetArchivedFirstSemesterEnrollmentAsync(enrollment.Email, academicYear);
                if (archivedFirst?.ExtraFields != null)
                {
                    foreach (var kvp in archivedFirst.ExtraFields)
                    {
                        if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                        {
                            var code = kvp.Key.Replace("SubjectRemarks.", "");
                            if (!firstSemRemarks.ContainsKey(code))
                            {
                                firstSemRemarks[code] = kvp.Value;
                                Console.WriteLine($"  - Loaded archived remark: {code} = {kvp.Value}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Enrolled2ndSemesterDetails] Error loading archived remarks: {ex.Message}");
            }

            // Extract 2nd semester remarks (editable)
            var secondSemRemarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ef.Where(x => x.Key.Contains(".2ndSem.", StringComparison.OrdinalIgnoreCase)))
            {
                var code = kv.Key.Replace("SubjectRemarks.2ndSem.", "");
                secondSemRemarks[code] = kv.Value;
            }

            // ✅ CRITICAL FIX: Get ACTUAL enrolled subjects from student schedule
            List<SubjectRow> firstSemSubjects = new();
            List<SubjectRow> secondSemSubjects = new();

            if (student != null)
            {
                // Get actual schedule
                var actualSchedule = await _db.GetStudentScheduleAsync(student.Username);

                if (actualSchedule != null && actualSchedule.Any())
                {
                    // Build subject dictionary for metadata lookup
                    var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);
                    if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                        foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }
                    else
                    {
                        foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                        foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }

                    // ✅ Build 2nd semester subjects from ACTUAL schedule
                    foreach (var meeting in actualSchedule)
                    {
                        var code = meeting.CourseCode ?? "";
                        if (string.IsNullOrWhiteSpace(code)) continue;

                        subjectDict.TryGetValue(code, out var meta);

                        secondSemSubjects.Add(new SubjectRow
                        {
                            Code = code,
                            Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                            Units = meta.Units,
                            PreRequisite = ""
                        });
                    }

                    Console.WriteLine($"[Enrolled2ndSemesterDetails] Loaded {secondSemSubjects.Count} subjects from schedule for {student.Username}");
                }
                else
                {
                    Console.WriteLine($"[Enrolled2ndSemesterDetails] No schedule found for {student.Username}, using canonical subjects");
                    // Fallback to canonical subjects
                    secondSemSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                        ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList()
                        : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
                }
            }
            else
            {
                Console.WriteLine($"[Enrolled2ndSemesterDetails] No student account found for {enrollment.Email}");
                // Fallback to canonical subjects
                secondSemSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
            }

            // Get 1st semester subjects (canonical - for prerequisite reference)
            firstSemSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList()
                : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList();

            // Get prerequisite map for 2nd year
            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);

            var vm = new SecondSemesterEnrolledViewModel
            {
                Enrollment = enrollment,
                StudentName = enrollment.FullName ?? "Unknown",
                Program = program,
                Email = enrollment.Email ?? "",
                FirstSemesterSubjects = firstSemSubjects,
                FirstSemesterRemarks = firstSemRemarks, // ✅ Now correctly populated
                SecondSemesterSubjects = secondSemSubjects, // ✅ Uses actual schedule
                SecondSemesterRemarks = secondSemRemarks,
                PrerequisiteMap = prereqMap
            };

            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSecondSemesterRemarks(
     string id,
     [FromForm] Dictionary<string, string> secondSemRemarks,
     [FromForm] Dictionary<string, string> secondYearEligibility,
     [FromForm] Dictionary<string, string>? flags = null) // ← new parameter to accept document flags
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid enrollment ID.";
                return RedirectToAction(nameof(Enrolled2ndSem));
            }

            var enrollment = await _db.GetEnrollmentRequestByIdAsync(id);
            if (enrollment == null)
            {
                TempData["Error"] = "Enrollment record not found.";
                return RedirectToAction(nameof(Enrolled2ndSem));
            }

            // Merge and persist posted document flags (if any). This re-uses the existing MergePostedFlags logic
            // which will only accept required keys for the request (transferee -> transferee set).
            if (flags != null && flags.Any())
            {
                MergePostedFlags(enrollment, flags);

                // Ensure DocumentFlags dictionary exists (MergePostedFlags will create it but be defensive)
                enrollment.DocumentFlags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // Save 2nd semester remarks with .2ndSem. prefix
            if (secondSemRemarks != null && secondSemRemarks.Any())
            {
                enrollment.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in secondSemRemarks)
                {
                    var code = (kv.Key ?? "").Trim();
                    var val = (kv.Value ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(code)) continue;

                    // normalize to allowed remark values
                    if (val != "pass" && val != "fail" && val != "ongoing") val = string.IsNullOrEmpty(val) ? "ongoing" : val;

                    enrollment.ExtraFields[$"SubjectRemarks.2ndSem.{code}"] = val;
                }
            }

            // Save 2nd year eligibility
            if (secondYearEligibility != null && secondYearEligibility.Any())
            {
                enrollment.SecondSemesterEligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in secondYearEligibility)
                {
                    var key = (kv.Key ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) continue;
                    enrollment.SecondSemesterEligibility[key] = kv.Value ?? "";
                }
            }

            enrollment.LastUpdatedAt = DateTime.UtcNow;
            await _db.UpdateEnrollmentRequestAsync(enrollment);

            // Build user-friendly message
            var savedParts = new List<string>();
            if (flags != null && flags.Any()) savedParts.Add("Document flags");
            if (secondSemRemarks != null && secondSemRemarks.Any()) savedParts.Add("2nd semester remarks");
            if (secondYearEligibility != null && secondYearEligibility.Any()) savedParts.Add("2nd year eligibility");

            if (savedParts.Count > 0)
                TempData["Success"] = $"✅ {string.Join(" and ", savedParts)} successfully saved.";
            else
                TempData["Info"] = "No changes were submitted.";

            return RedirectToAction(nameof(Enrolled2ndSemesterDetails), new { id });
        }


        [HttpGet]
        public async Task<IActionResult> DebugSecondSemEnrollment(string id)
        {
            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null) return NotFound("Request not found");

            var student = await _db.GetStudentByEmailAsync(request.Email);
            var program = GetProgramForRequest(request);

            var eligibleSubjects = new List<string>();
            if (request.SecondSemesterEligibility != null && request.SecondSemesterEligibility.Any())
            {
                foreach (var kvp in request.SecondSemesterEligibility)
                {
                    if (kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                    {
                        eligibleSubjects.Add(kvp.Key);
                    }
                }
            }

            return Ok(new
            {
                requestId = request.Id,
                email = request.Email,
                studentExists = student != null,
                studentUsername = student?.Username,
                program,
                hasEligibility = request.SecondSemesterEligibility != null,
                eligibilityCount = request.SecondSemesterEligibility?.Count ?? 0,
                eligibleSubjectsCount = eligibleSubjects.Count,
                eligibleSubjects = string.Join(", ", eligibleSubjects),
                allEligibility = request.SecondSemesterEligibility
            });
        }
        [HttpGet]
        public async Task<IActionResult> Archive(string? ay = null)
        {
            Console.WriteLine($"[Archive Action] AY filter: '{ay ?? "NONE"}'");

            var items = await _db.GetArchivesAsync(ay);

            Console.WriteLine($"[Archive Action] Retrieved {items.Count} records from database");

            // ✅ DEBUG: Show what we're passing to the view
            if (items.Any())
            {
                Console.WriteLine("First 3 records:");
                foreach (var item in items.Take(3))
                {
                    Console.WriteLine($"  - {item.FullName}: Sem='{item.Semester}', AY='{item.AcademicYear}'");
                }
            }

            ViewData["Title"] = "Archived Enrollments";
            ViewBag.FilterAY = ay ?? "";
            return View("~/Areas/Admin/Views/Admin/Archive.cshtml", items);
        }

        //  Archive details
        [HttpGet]
        public async Task<IActionResult> ArchiveDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid archive id.";
                return RedirectToAction(nameof(Archive));
            }

            var item = await _db.GetArchiveByIdAsync(id);
            if (item == null)
            {
                TempData["Error"] = "Archived record not found.";
                return RedirectToAction(nameof(Archive));
            }

            ViewData["Title"] = "Archive Details";
            return View("~/Areas/Admin/Views/Admin/ArchiveDetails.cshtml", item);
        }

        [HttpGet]
        public async Task<IActionResult> Search(string q, int take = 8)
        {
            var term = (q ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(term))
                return Ok(new { items = Array.Empty<object>() });

            var results = new List<EnrollmentRequest>();
            var byIdPattern = Regex.IsMatch(term, @"^\d{4}-\d{4}$");

            if (byIdPattern)
            {
                var student = await _db.GetStudentByUsernameAsync(term);
                if (student != null && !string.IsNullOrWhiteSpace(student.Email))
                {
                    var req = await _db.GetLatestRequestByEmailAsync(student.Email);
                    if (req != null) results.Add(req);
                }
            }
            else if (term.Contains("@"))
            {
                var req = await _db.GetLatestRequestByEmailAsync(term.ToLowerInvariant());
                if (req != null) results.Add(req);
            }

            var nameMatches = await _db.SearchEnrollmentRequestsByNameAsync(term, limit: take);
            results.AddRange(nameMatches);

            // Deduplicate by Id and order by latest
            var dedup = results
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .OrderByDescending(r => r.SubmittedAt)
                .Take(Math.Max(1, take))
                .ToList();

            var items = dedup.Select(r => new
            {
                id = r.Id,
                fullName = r.FullName,
                email = r.Email,
                status = r.Status,
                type = r.Type,
                submittedAt = r.SubmittedAt,
                link = Url.Action("RequestDetails", "Admin", new { area = "Admin", id = r.Id })
            });

            return Ok(new { items });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAcademicYear(string academicYear)
        {
            academicYear = (academicYear ?? "").Trim();
            var settings = await _db.GetEnrollmentSettingsAsync();
            settings.AcademicYear = academicYear;
            await _db.UpsertEnrollmentSettingsAsync(settings);

            TempData["Info"] = $"Academic Year updated to {(string.IsNullOrWhiteSpace(academicYear) ? "(blank)" : academicYear)}.";
            return RedirectToAction("AdminSettings", "Admin", new { area = "Admin" });
        }
        // NEW: Upsert a single program capacity (e.g., "BSIT" -> 120)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProgramCapacity(string program, int capacity)
        {
            var key = (program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                TempData["Error"] = "Program is required.";
                return RedirectToAction(nameof(AdminSettings));
            }

            if (!ProgramCatalog.IsSupported(key))
            {
                TempData["Error"] = $"Unsupported program '{key}'.";
                return RedirectToAction(nameof(AdminSettings));
            }

            if (capacity < 0) capacity = 0;

            // Normalize to catalog code casing
            var normalizedCode = ProgramCatalog.All.First(p => p.Code.Equals(key, StringComparison.OrdinalIgnoreCase)).Code;

            var settings = await _db.GetEnrollmentSettingsAsync();
            settings.ProgramCapacities ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            settings.ProgramCapacities[normalizedCode] = capacity;
            await _db.UpsertEnrollmentSettingsAsync(settings);

            // NEW: Try to auto-accept any waitlisted (On Hold with "Program capacity reached")
            var autoAccepted = await AutoAcceptWaitlistedForProgramAsync(normalizedCode);

            var capText = capacity == 0 ? "unlimited" : capacity.ToString();
            var msg = $"Capacity for program '{normalizedCode}' set to {capText}.";
            if (autoAccepted > 0)
                msg += $" Auto-enrolled {autoAccepted} request(s) from waitlist.";
            TempData["Info"] = msg;

            return RedirectToAction(nameof(AdminSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProgramCapacity(string program)
        {
            var key = (program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key) || !ProgramCatalog.IsSupported(key))
            {
                TempData["Error"] = "Program capacity not found.";
                return RedirectToAction(nameof(AdminSettings));
            }

            var normalizedCode = ProgramCatalog.All.First(p => p.Code.Equals(key, StringComparison.OrdinalIgnoreCase)).Code;

            var settings = await _db.GetEnrollmentSettingsAsync();
            if (settings.ProgramCapacities?.Remove(normalizedCode) == true)
            {
                await _db.UpsertEnrollmentSettingsAsync(settings);

                // NEW: With no capacity limit, accept all waitlisted for this program
                var autoAccepted = await AutoAcceptWaitlistedForProgramAsync(normalizedCode);

                var msg = $"Capacity for program '{normalizedCode}' removed.";
                if (autoAccepted > 0)
                    msg += $" Auto-enrolled {autoAccepted} request(s) from waitlist.";
                TempData["Info"] = msg;
            }
            else
            {
                TempData["Error"] = "Program capacity not found.";
            }
            return RedirectToAction(nameof(AdminSettings));
        }

        private async Task<int> AutoAcceptWaitlistedForProgramAsync(string program)
        {
            var prog = (program ?? "").Trim();
            if (string.IsNullOrEmpty(prog)) return 0;

            var settings = await _db.GetEnrollmentSettingsAsync();

            int cap = 0;
            var hasCap = settings.ProgramCapacities != null &&
                         settings.ProgramCapacities.TryGetValue(prog, out cap) &&
                         cap > 0;

            int accepted = 0;

            const int BatchSize = 10;
            while (true)
            {
                long enrolled = await _db.CountEnrolledByProgramAsync(prog);
                long available = hasCap ? (cap - enrolled) : long.MaxValue;

                if (available <= 0) break;

                var take = hasCap ? (int)Math.Min(BatchSize, available) : BatchSize;
                var candidates = await _db.GetOnHoldByProgramAndReasonAsync(prog, "Program capacity reached", take);
                if (candidates.Count == 0) break;

                foreach (var req in candidates)
                {
                    if (!string.Equals(req.Status, "On Hold", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.Equals(req.Reason ?? "", "Program capacity reached", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!AreFlagsComplete(req)) continue;

                    if (hasCap)
                    {
                        enrolled = await _db.CountEnrolledByProgramAsync(prog);
                        if (enrolled >= cap) break;
                    }

                    bool ok = IsSecondSemRequest(req)
                        ? await EnrollRequestSecondSemAsync(req)
                        : await EnrollRequestCoreAsync(req, attachRegFormPdf: true);

                    if (ok) accepted++;
                }

                if (candidates.Count < take) break;
            }

            return accepted;
        }
        private async Task<bool> EnrollRequestCoreAsync(EnrollmentRequest request, bool attachRegFormPdf = false)
        {
            try
            {
                // ✅ Check if student already exists (for transferees who might have accounts)
                var existingStudent = await _db.GetStudentByEmailAsync(request.Email);

                E_SysV0._01.Models.Student student; string? tempPassword = null;
                bool createdNewAccount = false;

                if (existingStudent != null)
                {
                    // Use existing student account (e.g., for transferees)
                    student = existingStudent;
                    Console.WriteLine($"[EnrollRequestCoreAsync] Using existing student account: {student.Username}");
                }
                else
                {
                    // Create new student account
                    var year = DateTime.UtcNow.Year;
                    var tempUsername = await _db.GenerateNextStudentUsernameAsync(year);
                    tempPassword = Guid.NewGuid().ToString("N")[..8];
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

                    student = new E_SysV0._01.Models.Student
                    {
                        Username = tempUsername,
                        PasswordHash = passwordHash,
                        FirstLogin = true,
                        Email = (request.Email ?? string.Empty).Trim().ToLowerInvariant(),
                        Type = request.Type
                    };

                    await _db.CreateStudentAsync(student);
                    createdNewAccount = true;
                    Console.WriteLine($"[EnrollRequestCoreAsync] Created new student account: {student.Username}");
                }

                var settings = await _db.GetEnrollmentSettingsAsync();

                // Determine which semester the request is for
                var requestSemester = "1st Semester"; // Default

                if (request.ExtraFields != null &&
                    request.ExtraFields.TryGetValue("Academic.Semester", out var reqSem) &&
                    !string.IsNullOrWhiteSpace(reqSem))
                {
                    requestSemester = reqSem.Trim();
                }

                var program = GetProgramForRequest(request);

                // ✅ FIX: Use Contains instead of Equals to catch "Transferee", "Transferee-Regular", "Transferee-Irregular"
                var isTransferee = !string.IsNullOrWhiteSpace(request.Type) &&
                    request.Type.Contains("Transferee", StringComparison.OrdinalIgnoreCase);

                Console.WriteLine($"[EnrollRequestCoreAsync] Type='{request.Type}', isTransferee={isTransferee}"); // ✅ DEBUG



                // ✅ Get subjects to assign
                List<string> subjectsToAssign;

                if (isTransferee && request.SecondSemesterEligibility != null && request.SecondSemesterEligibility.Any())
                {
                    // ✅ TRANSFEREE: Use eligible subjects from calculated eligibility
                    subjectsToAssign = request.SecondSemesterEligibility
                        .Where(kvp => kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    Console.WriteLine($"[EnrollRequestCoreAsync] Transferee: Assigning {subjectsToAssign.Count} eligible 2nd sem subjects: {string.Join(", ", subjectsToAssign)}");
                }
                else if (requestSemester.StartsWith("1", StringComparison.OrdinalIgnoreCase))
                {
                    // ✅ FRESHMEN 1ST SEMESTER: Assign all 1st semester subjects
                    if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        subjectsToAssign = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                            .Select(s => s.Code)
                            .ToList();
                    }
                    else
                    {
                        subjectsToAssign = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects
                            .Select(s => s.Code)
                            .ToList();
                    }

                    Console.WriteLine($"[EnrollRequestCoreAsync] Freshmen 1st Semester: Assigning {subjectsToAssign.Count} subjects");
                }
                else
                {
                    // ✅ FRESHMEN 2ND SEMESTER (NON-TRANSFEREE): Assign all 2nd semester subjects
                    if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        subjectsToAssign = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects
                            .Select(s => s.Code)
                            .ToList();
                    }
                    else
                    {
                        subjectsToAssign = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects
                            .Select(s => s.Code)
                            .ToList();
                    }

                    Console.WriteLine($"[EnrollRequestCoreAsync] Freshmen 2nd Semester: Assigning {subjectsToAssign.Count} subjects");
                }

                if (!subjectsToAssign.Any())
                {
                    Console.WriteLine($"[EnrollRequestCoreAsync] ERROR: No subjects to assign!");
                    if (createdNewAccount) await _db.DeleteStudentByUsernameAsync(student.Username);
                    return false;
                }

                // Create custom section
                try
                {
                    await _db.ClearStudentScheduleAsync(student.Username);

                    var sectionName = isTransferee
                        ? $"{program}-Transferee-{student.Username}"
                        : $"{program}-{student.Username}-{requestSemester.Replace(" ", "")}";

                    var customSection = new CourseSection
                    {
                        Id = $"{program}-{DateTime.UtcNow.Year}-{student.Username}",
                        Program = program,
                        Name = sectionName,
                        Capacity = 1,
                        CurrentCount = 0,
                        Year = DateTime.UtcNow.Year
                    };

                    await _db.CreateCustomSectionAsync(customSection);
                    await _db.GenerateSectionScheduleAsync(customSection.Id, subjectsToAssign);
                    await _db.EnrollStudentInSectionAsync(student.Username, customSection.Id);

                    var meetings = await _db.GetStudentScheduleAsync(student.Username);
                    if (meetings == null || meetings.Count == 0)
                    {
                        await _db.RollbackSectionAssignmentAsync(student.Username);
                        if (createdNewAccount) await _db.DeleteStudentByUsernameAsync(student.Username);
                        return false;
                    }

                    Console.WriteLine($"[EnrollRequestCoreAsync] Assigned {meetings.Count} subjects to {student.Username}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequestCoreAsync] Error assigning section: {ex.Message}");
                    if (createdNewAccount) await _db.DeleteStudentByUsernameAsync(student.Username);
                    return false;
                }

                // Send acceptance email
                try
                {
                    byte[]? pdfBytes = null;
                    if (attachRegFormPdf)
                    {
                        try { pdfBytes = await _pdf.GenerateForRequestAsync(request.Id); }
                        catch { /* proceed without attachment */ }
                    }

                    // ✅ Only send password if we created a new account
                    if (createdNewAccount && !string.IsNullOrWhiteSpace(tempPassword))
                    {
                        await _email.SendAcceptanceEmailAsync(request.Email, student.Username, tempPassword, pdfBytes);
                    }
                    else
                    {
                        // Existing account - send enrollment confirmation without password
                        await _email.SendAcceptanceEmailAsync(request.Email, student.Username, null!, pdfBytes);
                    }
                }
                catch
                {
                    await _db.RollbackSectionAssignmentAsync(student.Username);
                    if (createdNewAccount) await _db.DeleteStudentByUsernameAsync(student.Username);
                    return false;
                }

                // Update request status
                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                request.ExtraFields["Academic.Semester"] = requestSemester;
                request.ExtraFields["EnrolledSubjects"] = string.Join(",", subjectsToAssign);

                bool isIrregular = request.Type != null &&
                    (request.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase) ||
                     request.Type.Equals("Transferee", StringComparison.OrdinalIgnoreCase));

                request.Status = isIrregular ? "Enrolled - Irregular" : "Enrolled";
                request.LastUpdatedAt = DateTime.UtcNow;
                request.EditToken = null;
                request.EditTokenExpires = null;
                await _db.UpdateEnrollmentRequestAsync(request);

                // IMPORTANT: If this enrollment is the 2nd Semester of 1st Year, keep the student's canonical Type as Freshmen-*
                try
                {
                    var yearLevel = request.ExtraFields.TryGetValue("Academic.YearLevel", out var yl) ? (yl ?? "") : "";
                    var sem = request.ExtraFields.TryGetValue("Academic.Semester", out var s) ? (s ?? "") : "";

                    var isFirstYearSecondSem = yearLevel.Contains("1st", StringComparison.OrdinalIgnoreCase) &&
                                               sem.Contains("2nd", StringComparison.OrdinalIgnoreCase);

                    if (isFirstYearSecondSem)
                    {
                        var newType = isIrregular ? "Freshmen-Irregular" : "Freshmen-Regular";
                        if (!string.Equals(student.Type ?? "", newType, StringComparison.OrdinalIgnoreCase))
                        {
                            student.Type = newType;
                            await _db.UpdateStudentAsync(student);
                            Console.WriteLine($"[EnrollRequestCoreAsync] Updated student.Type = {newType} for {student.Username}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequestCoreAsync] Warning: failed to update student.Type: {ex.Message}");
                }

                Console.WriteLine($"[EnrollRequestCoreAsync] ✅ Successfully enrolled {student.Username}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnrollRequestCoreAsync] Unexpected error: {ex.Message}");
                return false;
            }
        }

        private List<SubjectRow> GetSubjectsForYearAndSemester(string program, string yearLevel, string semester)
        {
            var prog = NormalizeProgramCode(program);
            var year = EnrollmentRules.ParseYearLevel(yearLevel);
            var isBSENT = string.Equals(prog, "BSENT", StringComparison.OrdinalIgnoreCase);
            var is1stSem = semester.Contains("1st", StringComparison.OrdinalIgnoreCase);

            // BSIT subjects
            if (!isBSENT)
            {
                return year switch
                {
                    1 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                    1 => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                    2 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                    2 => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                    3 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._3rdYear._3rdYear1stSem.Subjects.ToList(),
                    3 => E_SysV0._01.Models.BSITSubjectModels._3rdYear._3rdYear2ndSem.Subjects.ToList(),
                    4 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._4thYear._4thYear1stSem.Subjects.ToList(),
                    4 => E_SysV0._01.Models.BSITSubjectModels._4thYear._4thYear2ndSem.Subjects.ToList(),
                    _ => E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList()
                };
            }

            // BSENT subjects
            return year switch
            {
                1 when is1stSem => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList(),
                1 => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList(),
                2 when is1stSem => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                2 => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                _ => E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList()
            };
        }


        [HttpGet]
        public async Task<IActionResult> DebugStudentSchedule(string username)
        {
            var schedule = await _db.GetStudentScheduleAsync(username);
            var subjects = schedule?.Select(s => s.CourseCode).ToList() ?? new List<string>();

            return Ok(new
            {
                username,
                subjectCount = subjects.Count,
                subjects = string.Join(", ", subjects)
            });
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AdminLogin() => View();

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogin(string username, string password, bool rememberMe = false)
        {
            var adminUser = _config["Admin:Username"];
            var adminHash = _config["Admin:PasswordHash"];

            bool hashLooksValid = !string.IsNullOrWhiteSpace(adminHash) &&
                                  (adminHash.StartsWith("$2a$") || adminHash.StartsWith("$2b$") || adminHash.StartsWith("$2y$")) &&
                                  adminHash.Length >= 60;

            bool credentialsOk = false;
            if (!string.IsNullOrWhiteSpace(adminUser) &&
                string.Equals(username, adminUser, StringComparison.OrdinalIgnoreCase) &&
                hashLooksValid)
            {
                try { credentialsOk = BCrypt.Net.BCrypt.Verify(password, adminHash); }
                catch { credentialsOk = false; }
            }

            if (credentialsOk)
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, AdminRole) };
                var identity = new ClaimsIdentity(claims, AdminScheme);
                var principal = new ClaimsPrincipal(identity);

                var authProps = new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : (DateTimeOffset?)null
                };

                await HttpContext.SignInAsync(AdminScheme, principal, authProps);
                return RedirectToAction(nameof(Dashboard));
            }

            ViewBag.ErrorMessage = "Invalid username or password.";
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {


            // Fetch lists (unenhanced) so we can compute dashboard counts consistently
            var pending1 = await _db.GetEnrollmentRequestsByStatusAsync("1st Sem Pending");
            var pending2 = await _db.GetEnrollmentRequestsByStatusAsync("2nd Sem Pending");

            // Gather all enrolled variants so we can filter to 1st-semester 1st-year enrolled records
            var enrolledBase = new List<EnrollmentRequest>();
            enrolledBase.AddRange(await _db.GetEnrollmentRequestsByStatusAsync("Enrolled") ?? new List<EnrollmentRequest>());
            enrolledBase.AddRange(await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Regular") ?? new List<EnrollmentRequest>());
            enrolledBase.AddRange(await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Irregular") ?? new List<EnrollmentRequest>());


            var dedupedEnrolledAll = enrolledBase
                .Where(r => !string.IsNullOrWhiteSpace(r?.Email))
                .GroupBy(r => (r.Email ?? "").Trim().ToLowerInvariant())
                .Select(g => g.OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt).First())
                .ToList();

            // Count by year-level using ExtraFields first, fallback to Type/Status
            int enrolled1stYearCount = dedupedEnrolledAll.Count(r =>
            {
                string yl = "";
                if (r?.ExtraFields != null && r.ExtraFields.TryGetValue("Academic.YearLevel", out var v)) yl = v ?? "";
                if (string.IsNullOrWhiteSpace(yl) && !string.IsNullOrWhiteSpace(r?.Type)) yl = r.Type;
                if (string.IsNullOrWhiteSpace(yl) && !string.IsNullOrWhiteSpace(r?.Status)) yl = r.Status;
                return !string.IsNullOrWhiteSpace(yl) && yl.IndexOf("1st", StringComparison.OrdinalIgnoreCase) >= 0;
            });

            int enrolled2ndYearCount = dedupedEnrolledAll.Count(r =>
            {
                string yl = "";
                if (r?.ExtraFields != null && r.ExtraFields.TryGetValue("Academic.YearLevel", out var v)) yl = v ?? "";
                if (string.IsNullOrWhiteSpace(yl) && !string.IsNullOrWhiteSpace(r?.Type)) yl = r.Type;
                if (string.IsNullOrWhiteSpace(yl) && !string.IsNullOrWhiteSpace(r?.Status)) yl = r.Status;
                return !string.IsNullOrWhiteSpace(yl) && yl.IndexOf("2nd", StringComparison.OrdinalIgnoreCase) >= 0;
            });

            // Expose for the view
            ViewBag.Enrolled1stYearCount = enrolled1stYearCount;
            ViewBag.Enrolled2ndYearCount = enrolled2ndYearCount;

            // (optional) log for debugging
            Console.WriteLine($"[Dashboard] Deduped enrolled total: {dedupedEnrolledAll.Count}, 1stYear={enrolled1stYearCount}, 2ndYear={enrolled2ndYearCount}");


            // Filter to 1st-semester records (match the behavior of the Enrolled() action)
            var firstSemesterEnrolled = enrolledBase
                .Where(r => r.ExtraFields != null
                            && r.ExtraFields.TryGetValue("Academic.Semester", out var sem)
                            && sem != null
                            && sem.Contains("1st", StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.Email) // dedupe by email, keep latest
                .Select(g => g.OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt).First())
                .OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt)
                .ToList();

            // NEW: Filter to ALL 2nd Semester (both 1st Year AND 2nd Year)
            var secondSemesterEnrolled = enrolledBase
                .Where(r => r.ExtraFields != null
                            && r.ExtraFields.TryGetValue("Academic.Semester", out var sem2)
                            && sem2 != null
                            && sem2.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.Email)
                .Select(g => g.OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt).First())
                .ToList();

            // Improved counting: handle Status == "Enrolled" and also fall back to EnrollmentType / Type
            int secondRegularCount = secondSemesterEnrolled.Count(r =>
            {
                if (!string.IsNullOrWhiteSpace(r.Status) && r.Status.StartsWith("Enrolled - Regular", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (r.ExtraFields != null && r.ExtraFields.TryGetValue("EnrollmentType", out var et) && !string.IsNullOrWhiteSpace(et) && et.Contains("Regular", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(r.Type) && r.Type.Contains("Regular", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            });

            int secondIrregularCount = secondSemesterEnrolled.Count(r =>
            {
                if (!string.IsNullOrWhiteSpace(r.Status) && r.Status.StartsWith("Enrolled - Irregular", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (r.ExtraFields != null && r.ExtraFields.TryGetValue("EnrollmentType", out var et2) && !string.IsNullOrWhiteSpace(et2) && et2.Contains("Irregular", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!string.IsNullOrWhiteSpace(r.Type) && r.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase))
                    return true;

                return false;
            });

            // Always use the actual filtered list count for total (covers plain "Enrolled" status)
            var secondTotal = secondSemesterEnrolled.Count;

            Console.WriteLine($"[Dashboard] Enrolled 2nd Sem: {secondTotal} total ({secondRegularCount} regular, {secondIrregularCount} irregular)");
            // Build view model using the original pending / onhold / rejected lists, but use first-semester filtered lists for "Enrolled"
            var vm = new AdminDashboardViewModel
            {
                Pending1stSem = pending1 ?? new List<EnrollmentRequest>(),
                Pending2ndSem = pending2 ?? new List<EnrollmentRequest>(),
                Enrolled = firstSemesterEnrolled, // still used for the Enrolled (1st) card and Enrolled list
                EnrolledRegular = firstSemesterEnrolled
                                    .Where(r => !string.IsNullOrWhiteSpace(r.Status) && r.Status.StartsWith("Enrolled - Regular", StringComparison.OrdinalIgnoreCase))
                                    .ToList(),
                OnHold = await _db.GetEnrollmentRequestsByStatusAsync("On Hold") ?? new List<EnrollmentRequest>(),
                Rejected = await _db.GetEnrollmentRequestsByStatusAsync("Rejected") ?? new List<EnrollmentRequest>(),
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };

            // Keep irregular count consistent with the filtered first-semester list
            var enrolledIrregularCount = firstSemesterEnrolled.Count(r => !string.IsNullOrWhiteSpace(r.Status) && r.Status.StartsWith("Enrolled - Irregular", StringComparison.OrdinalIgnoreCase));
            ViewBag.EnrolledIrregularCount = enrolledIrregularCount;
            ViewBag.EnrolledIrregular = firstSemesterEnrolled.Where(r => r.Status != null && r.Status.StartsWith("Enrolled - Irregular", StringComparison.OrdinalIgnoreCase)).ToList();

            // NEW ViewBag values for 2nd semester metric (1st Year 2nd Sem)
            ViewBag.Enrolled2ndRegularCount = secondRegularCount;
            ViewBag.Enrolled2ndIrregularCount = secondIrregularCount;
            ViewBag.Enrolled2ndTotal = secondTotal;

            ViewBag.PendingShifterCount = (await _db.GetShifterEnrollmentRequestsByStatusAsync("Pending")).Count;
            ViewBag.AcceptedShifterCount = (await _db.GetShifterEnrollmentRequestsByStatusAsync("Accepted")).Count;
            ViewBag.RejectedShifterCount = (await _db.GetShifterEnrollmentRequestsByStatusAsync("Rejected")).Count;
            ViewBag.OnHoldShifterCount = (await _db.GetShifterEnrollmentRequestsByStatusAsync("On-Hold")).Count;

            ViewBag.StudentCountsByType = await _db.GetStudentCountsByTypeAsync();

            return View(vm);
        }


        private bool IsAjaxRequest()
        {
            if (Request?.Headers == null) return false;
            if (Request.Headers.TryGetValue("X-Requested-With", out var v))
                return string.Equals(v.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
            return false;
        }

        [HttpGet]
        public async Task<IActionResult> Pending1stSem(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetEnrollmentRequestsByStatusAsync("1st Sem Pending")
                : await _db.SearchRequestsByStatusAsync("1st Sem Pending", q, program);

            var vm = new EnrollmentListViewModel
            {
                Title = "1st Sem Pending Requests",
                Items = items,
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };
            ViewBag.FilterAction = "Pending1stSem";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            return View("Pending", vm);
        }

        [HttpGet]
        public async Task<IActionResult> Pending2ndSem(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetEnrollmentRequestsByStatusAsync("2nd Sem Pending")
                : await _db.SearchRequestsByStatusAsync("2nd Sem Pending", q, program);

            var vm = new EnrollmentListViewModel
            {
                Title = "2nd Sem Pending Requests",
                Items = items,
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };
            ViewBag.FilterAction = "Pending2ndSem";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            return View("Pending", vm);
        }
        [HttpGet]
        public async Task<IActionResult> Enrolled(string? q = null, string? program = null)
        {
            List<EnrollmentRequest> allEnrolled;

            if (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
            {
                // ✅ Fetch ALL enrolled records
                var enrolled = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled") ?? new List<EnrollmentRequest>();
                var regular = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Regular") ?? new List<EnrollmentRequest>();
                var irregular = await _db.GetEnrollmentRequestsByStatusAsync("Enrolled - Irregular") ?? new List<EnrollmentRequest>();

                allEnrolled = enrolled.Concat(regular).Concat(irregular).ToList();
            }
            else
            {
                var enrolled = await _db.SearchRequestsByStatusAsync("Enrolled", q, program) ?? new List<EnrollmentRequest>();
                var regular = await _db.SearchRequestsByStatusAsync("Enrolled - Regular", q, program) ?? new List<EnrollmentRequest>();
                var irregular = await _db.SearchRequestsByStatusAsync("Enrolled - Irregular", q, program) ?? new List<EnrollmentRequest>();

                allEnrolled = enrolled.Concat(regular).Concat(irregular).ToList();
            }

            // ✅ CRITICAL: Filter to show ONLY 1st Semester Records (not 2nd semester)
            var firstSemesterOnly = allEnrolled
                .Where(r => r.ExtraFields != null &&
                            r.ExtraFields.TryGetValue("Academic.Semester", out var sem) &&
                            sem.Contains("1st", StringComparison.OrdinalIgnoreCase))
                .GroupBy(r => r.Email) // ✅ Remove duplicates by email
                .Select(g => g.OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt).First())
                .OrderByDescending(r => r.LastUpdatedAt ?? r.SubmittedAt)
                .ToList();

            var vm = new EnrollmentListViewModel
            {
                Title = "Enrolled Students (1st Semester)",
                Items = firstSemesterOnly
            };

            Console.WriteLine($"[Enrolled] Showing {firstSemesterOnly.Count} 1st semester students (from {allEnrolled.Count} total enrolled)");

            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> OnHold(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetEnrollmentRequestsByStatusAsync("On Hold")
                : await _db.SearchRequestsByStatusAsync("On Hold", q, program);

            var vm = new EnrollmentListViewModel
            {
                Title = "On Hold Request",
                Items = items,
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };
            ViewBag.FilterAction = "OnHold";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            return View("OnHold", vm);

        }

        [HttpGet]
        public async Task<IActionResult> Rejected(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetEnrollmentRequestsByStatusAsync("Rejected")
                : await _db.SearchRequestsByStatusAsync("Rejected", q, program);

            var vm = new EnrollmentListViewModel
            {
                Title = "Rejected Request",
                Items = items,
                EnrollmentSettings = await _db.GetEnrollmentSettingsAsync()
            };
            ViewBag.FilterAction = "Rejected";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            return View("Rejected", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(
            string id,
            [FromForm] Dictionary<string, string>? flags,
            [FromForm] Dictionary<string, string>? eligibility,
            [FromForm] bool reviewed = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                if (IsAjaxRequest()) return BadRequest(new { success = false, message = "Invalid request id. but Accepted 1" });
                TempData["Error"] = "Invalid request id. but Accepted 1";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                if (IsAjaxRequest()) return BadRequest(new { success = false, message = "Enrollment request not found. but Accepted 2" });
                TempData["Error"] = "Enrollment request not found. but Accepted 2";
                return RedirectToAction(nameof(Dashboard));
            }

            var originalStatus = (request.Status ?? "").Trim();
            var originalType = (request.Type ?? "").Trim();

            if (!reviewed)
            {
                var msg = "Please open Review and confirm you have reviewed the request.";
                if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            var settings = await _db.GetEnrollmentSettingsAsync();
            MergePostedFlags(request, flags);

            try
            {
                await _db.UpdateEnrollmentRequestAsync(request);
                Console.WriteLine($"[Accept] ✅ Merged document flags persisted for request {request.Id} (flags count: {request.DocumentFlags?.Count ?? 0})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Accept] ❌ Failed to persist merged flags for request {request.Id}: {ex.Message}");
                // proceed — later checks will fail if flags aren't complete, giving the admin a clear error
            }


            var isTransfereeOriginal = !string.IsNullOrWhiteSpace(originalType) &&
                                       originalType.Equals("Transferee", StringComparison.OrdinalIgnoreCase);

            // ✅ CRITICAL FIX: Update eligibility BEFORE enrollment with null-safe handling
            if (isTransfereeOriginal && eligibility != null && eligibility.Any())
            {
                // ✅ Filter out null or empty values before saving
                var sanitizedEligibility = eligibility
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                if (sanitizedEligibility.Any())
                {
                    request.SecondSemesterEligibility = sanitizedEligibility;
                    await _db.UpdateEnrollmentRequestAsync(request);

                    Console.WriteLine($"[Accept] ✅ Updated transferee eligibility BEFORE enrollment: {sanitizedEligibility.Count} subjects");

                    // ✅ NULL-SAFE: Filter eligible subjects with proper null checking
                    var eligibleSubjects = sanitizedEligibility
                        .Where(kvp => kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        .Select(kvp => kvp.Key)
                        .ToList();

                    Console.WriteLine($"[Accept] Eligible subjects for enrollment: {string.Join(", ", eligibleSubjects)}");

                    // ✅ CRITICAL: Verify the data was saved
                    var verifyRequest = await _db.GetEnrollmentRequestByIdAsync(id);
                    if (verifyRequest?.SecondSemesterEligibility != null)
                    {
                        Console.WriteLine($"[Accept] ✅ VERIFICATION: Eligibility saved correctly ({verifyRequest.SecondSemesterEligibility.Count} entries)");
                    }
                    else
                    {
                        Console.WriteLine($"[Accept] ❌ ERROR: Eligibility was NOT saved to database!");
                    }
                }
                else
                {
                    Console.WriteLine($"[Accept] ⚠️ Warning: Eligibility dictionary was empty after sanitization");
                }
            }
            else
            {
                // ✅ Log why eligibility wasn't updated
                if (!isTransfereeOriginal)
                    Console.WriteLine($"[Accept] Skipping eligibility update - not a transferee (Type: {originalType})");
                else if (eligibility == null || !eligibility.Any())
                    Console.WriteLine($"[Accept] ⚠️ Warning: Transferee request but no eligibility data received from form");
            }

            if (isTransfereeOriginal)
            {
                // ✅ Ensure semester is set
                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (!request.ExtraFields.ContainsKey("Academic.Semester"))
                {
                    request.ExtraFields["Academic.Semester"] = "2nd Semester";
                }

                if (!request.ExtraFields.ContainsKey("Academic.YearLevel"))
                {
                    request.ExtraFields["Academic.YearLevel"] = "1st Year";
                }

                // ✅ Verify remarks are present
                var remarkCount = request.ExtraFields.Count(kvp =>
                    kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase));

                Console.WriteLine($"[Accept] Transferee has {remarkCount} subject remarks");

                if (remarkCount == 0)
                {
                    var msg = "Subject remarks not found. Please calculate eligibility first.";
                    if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }

                await _db.UpdateEnrollmentRequestAsync(request);
            }

            if (isTransfereeOriginal)
            {
                try
                {
                    var allFirstYearRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(request, _db);
                    var regularity = EnrollmentRules.DetermineRegularity(allFirstYearRemarks);

                    request.Type = string.Equals(regularity, "Irregular", StringComparison.OrdinalIgnoreCase)
                        ? "Transferee-Irregular"
                        : "Transferee-Regular";

                    await _db.UpdateEnrollmentRequestAsync(request);
                    Console.WriteLine($"[Accept] Normalized transferee request.Type -> {request.Type} for {request.Id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Accept] Warning: failed to determine transferee regularity: {ex.Message}");
                }
            }

            // [Keep all existing ROUTE 1 and ROUTE 2 code for 2nd year enrollment]
            var is2ndYear2ndSemEnrollment = !string.IsNullOrWhiteSpace(request.Type) &&
                 (request.Type.Contains("Sophomore-Regular", StringComparison.OrdinalIgnoreCase) ||
                  request.Type.Contains("Sophomore-Irregular", StringComparison.OrdinalIgnoreCase)) &&
                 request.ExtraFields != null &&
                 request.ExtraFields.TryGetValue("Academic.YearLevel", out var yl) &&
                 yl.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                 request.ExtraFields.TryGetValue("Academic.Semester", out var sem) &&
                 sem.Contains("2nd", StringComparison.OrdinalIgnoreCase);

            if (is2ndYear2ndSemEnrollment)
            {
                Console.WriteLine($"[Accept] Processing 2nd Year 2nd Semester enrollment for {request.Email}");
                bool success = await EnrollRequest2ndYear2ndSemAsync(request);
                if (!success)
                {
                    var msg = "2nd Year 2nd Semester enrollment failed. Please check eligibility and subject remarks.";
                    if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
                if (IsAjaxRequest())
                    return Ok(new { success = true, message = "2nd Year 2nd Semester enrollment successful" });
                TempData["Success"] = "✅ 2nd Year 2nd Semester enrollment completed successfully!";
                return RedirectToAction(nameof(Dashboard));
            }

            var is2ndYearEnrollment = !string.IsNullOrWhiteSpace(request.Type) &&
                (request.Type.Contains("2nd Year-Regular", StringComparison.OrdinalIgnoreCase) ||
                 request.Type.Contains("2nd Year-Irregular", StringComparison.OrdinalIgnoreCase));

            if (is2ndYearEnrollment)
            {
                Console.WriteLine($"[Accept] Processing 2nd Year 1st Semester enrollment (from 1st year) for {request.Email}");
                if (!AreFlagsComplete(request))
                {
                    Console.WriteLine($"[Accept] Warning: Document flags seem incomplete for 2nd Year request {request.Id}. Flags will be preserved as-is.");
                }
                bool success = await EnrollRequest2ndYearAsync(request);
                if (!success)
                {
                    var msg = "2nd Year enrollment failed. Please check eligibility and subject remarks.";
                    if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
                if (IsAjaxRequest())
                    return Ok(new { success = true, message = "2nd Year enrollment successful" });
                TempData["Success"] = "✅ 2nd Year enrollment completed successfully!";
                return RedirectToAction(nameof(Dashboard));
            }

            var isSecondSem = !isTransfereeOriginal && IsSecondSemRequest(request);

            if (!isSecondSem || isTransfereeOriginal)
            {
                MergePostedFlags(request, flags);
                if (!AreFlagsComplete(request))
                {
                    var msg = "Flag all required documents before accepting the request.";
                    if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                if (IsAjaxRequest()) return Ok(new { success = true, alreadyEnrolled = true });
                TempData["Info"] = "Request already marked as Enrolled.";
                return RedirectToAction(nameof(Dashboard));
            }

            var program = (request.Program ?? "").Trim();
            if (!string.IsNullOrEmpty(program) &&
                settings.ProgramCapacities != null &&
                settings.ProgramCapacities.TryGetValue(program, out var cap) &&
                cap > 0)
            {
                var enrolled = await _db.CountEnrolledByProgramAsync(program);
                if (enrolled >= cap)
                {
                    request.Status = "On Hold";
                    request.Reason = "Program capacity reached";
                    request.LastUpdatedAt = DateTime.UtcNow;
                    await _db.UpdateEnrollmentRequestAsync(request);
                    var msg = "Program capacity reached. Request placed On Hold.";
                    if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToListFor(request);
                }
            }

            // ✅ CRITICAL: Reload request before enrollment to ensure we have the latest data
            request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                var msg = "Failed to reload request data before enrollment";
                if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(Dashboard));
            }

            bool enrollSuccess;
            if (isSecondSem && !isTransfereeOriginal)
            {
                enrollSuccess = await EnrollRequestSecondSemAsync(request);
            }
            else
            {
                // ✅ Verify eligibility exists before enrollment
                if (isTransfereeOriginal)
                {
                    if (request.SecondSemesterEligibility == null || !request.SecondSemesterEligibility.Any())
                    {
                        Console.WriteLine($"[Accept] ❌ ERROR: Transferee request has no eligibility data!");
                        var msg = "Eligibility calculation required before enrollment. Please calculate eligibility first.";
                        if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                        TempData["Error"] = msg;
                        return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                    }
                    Console.WriteLine($"[Accept] ✅ Confirmed eligibility exists ({request.SecondSemesterEligibility.Count} subjects) before calling EnrollRequestCoreAsync");
                }

                enrollSuccess = await EnrollRequestCoreAsync(request, attachRegFormPdf: false);
            }

            if (!enrollSuccess)
            {
                var msg = isSecondSem && !isTransfereeOriginal
                    ? "Second semester enrollment failed."
                    : isTransfereeOriginal
                    ? "Transferee enrollment failed."
                    : "Enrollment failed.";
                if (IsAjaxRequest()) return BadRequest(new { success = false, message = msg });
                TempData["Error"] = msg;
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            if (isTransfereeOriginal)
            {
                try
                {
                    var student = await _db.GetStudentByEmailAsync(request.Email);
                    if (student != null && !string.IsNullOrWhiteSpace(request.Type))
                    {
                        if (!string.Equals(student.Type ?? "", request.Type, StringComparison.OrdinalIgnoreCase))
                        {
                            student.Type = request.Type;
                            await _db.UpdateStudentAsync(student);
                            Console.WriteLine($"[Accept] Updated student.Type = {student.Type} for {student.Username}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Accept] Warning: failed to update student.Type for transferee: {ex.Message}");
                }
            }

            var origIsFreshmen = !string.IsNullOrWhiteSpace(originalType) &&
                                originalType.StartsWith("Freshmen", StringComparison.OrdinalIgnoreCase);

            var showRegistrationSlip =
                (origIsFreshmen && string.Equals(originalStatus, "1st Sem Pending", StringComparison.OrdinalIgnoreCase))
                || (isTransfereeOriginal && string.Equals(originalStatus, "2nd Sem Pending", StringComparison.OrdinalIgnoreCase));

            if (IsAjaxRequest())
            {
                var regUrl = Url.Action("RegistrationSlip", "Admin", new { area = "Admin", id = request.Id });
                return Ok(new { success = true, showRegistrationSlip, registrationUrl = regUrl });
            }

            TempData["Success"] = showRegistrationSlip
                ? "✅ Student enrolled. Showing registration slip..."
                : (isSecondSem && !isTransfereeOriginal
                    ? "✅ Student enrolled for 2nd Semester."
                    : isTransfereeOriginal
                    ? "✅ Transferee enrolled successfully."
                    : "✅ Student enrolled.");

            if (showRegistrationSlip)
            {
                if (!string.IsNullOrWhiteSpace(request.Status) &&
                    request.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction(nameof(EnrolledDetails), new { id = request.Id, showSlip = "1" });
                }
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id, showSlip = "1" });
            }

            return RedirectToAction(nameof(EnrolledDetails));
        }




        private async Task<bool> EnrollRequest2ndYear2ndSemAsync(EnrollmentRequest currentRequest)
        {
            try
            {
                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Starting 2nd Year 2nd Sem enrollment for {currentRequest.Email}");

                var student = await _db.GetStudentByEmailAsync(currentRequest.Email);
                if (student == null)
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Student not found");
                    return false;
                }

                var program = GetProgramForRequest(currentRequest);
                var settings = await _db.GetEnrollmentSettingsAsync();

                // ✅ UPDATED: Get subjects from student's selection (includes retakes)
                var eligibleSubjects = new List<string>();

                // Priority 1: Use student-selected subjects from EnrolledSubjects
                if (currentRequest.ExtraFields != null)
                {
                    string? enrolledCsv = null;

                    // Try "EnrolledSubjects" first (used by irregular enrollment)
                    if (!currentRequest.ExtraFields.TryGetValue("EnrolledSubjects", out enrolledCsv) ||
                        string.IsNullOrWhiteSpace(enrolledCsv))
                    {
                        // Fallback to "SelectedSubjects" (used by 2nd year enrollment)
                        currentRequest.ExtraFields.TryGetValue("SelectedSubjects", out enrolledCsv);
                    }

                    if (!string.IsNullOrWhiteSpace(enrolledCsv))
                    {
                        eligibleSubjects.AddRange(enrolledCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                        Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Using student-selected subjects: {string.Join(", ", eligibleSubjects)}");
                    }
                }

                // Priority 2: Fallback to eligibility map (only if no subjects from ExtraFields)
                if (!eligibleSubjects.Any() &&
                    currentRequest.SecondSemesterEligibility != null &&
                    currentRequest.SecondSemesterEligibility.Any())
                {
                    foreach (var kvp in currentRequest.SecondSemesterEligibility)
                    {
                        if (kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        {
                            eligibleSubjects.Add(kvp.Key);
                        }
                    }
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Using eligibility map subjects: {string.Join(", ", eligibleSubjects)}");
                }

                // ✅ CRITICAL: Remove duplicates before validation
                eligibleSubjects = eligibleSubjects.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] ✅ Final subject list (after deduplication): {string.Join(", ", eligibleSubjects)}");

                // Validation: Ensure we have subjects to assign
                if (!eligibleSubjects.Any())
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] No eligible subjects found");
                    return false;
                }

                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Eligible for {eligibleSubjects.Count} subjects: {string.Join(", ", eligibleSubjects)}");

                // Preserve document flags
                Dictionary<string, string>? preservedFlags = null;
                if (currentRequest.DocumentFlags != null && currentRequest.DocumentFlags.Any())
                {
                    preservedFlags = new Dictionary<string, string>(currentRequest.DocumentFlags, StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Preserved {preservedFlags.Count} document flags");
                }

                // Archive current 2nd Year 1st Semester enrollment
                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Archiving 2nd Year 1st Sem record");
                try
                {
                    var allRequests = await _db.GetRequestsByEmailAsync(currentRequest.Email);
                    var current2ndYear1stSem = allRequests
                        .Where(r => r.Id != currentRequest.Id &&
                                   r.Status != null &&
                                   r.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase) &&
                                   r.ExtraFields != null &&
                                   r.ExtraFields.GetValueOrDefault("Academic.YearLevel", "").Contains("2nd Year") &&
                                   r.ExtraFields.GetValueOrDefault("Academic.Semester", "").Contains("1st"))
                        .ToList();

                    foreach (var old in current2ndYear1stSem)
                    {
                        Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Archiving {old.Id}");
                        await _db.ArchiveEnrollmentRequestAsync(old, "Advanced to 2nd Year 2nd Semester");
                        await _db.DeleteEnrollmentRequestAsync(old.Id);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Archiving warning: {ex.Message}");
                }

                // Clear and reassign schedule
                await _db.ClearStudentScheduleAsync(student.Username);

                var sectionName = $"{program}-2ndYear2ndSem-{student.Username}-{DateTime.UtcNow.Year}";
                var customSection = new CourseSection
                {
                    Id = $"{program}-{DateTime.UtcNow.Year}-{student.Username}-2Y2S",
                    Program = program,
                    Name = sectionName,
                    Capacity = 1,
                    CurrentCount = 0,
                    Year = DateTime.UtcNow.Year
                };

                await _db.CreateCustomSectionAsync(customSection);
                await _db.GenerateSectionScheduleAsync(customSection.Id, eligibleSubjects);
                await _db.EnrollStudentInSectionAsync(student.Username, customSection.Id);

                var schedule = await _db.GetStudentScheduleAsync(student.Username);
                if (schedule == null || !schedule.Any())
                {
                    await _db.DeleteSectionAsync(customSection.Id);
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Schedule generation failed");
                    return false;
                }

                // Determine regularity from 2nd Year 1st Semester remarks
                var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(currentRequest, _db);

                // Get 2nd Year 1st Sem remarks
                if (currentRequest.ExtraFields != null)
                {
                    foreach (var kvp in currentRequest.ExtraFields)
                    {
                        if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                        {
                            var code = kvp.Key.Replace("SubjectRemarks.", "");
                            if (!allRemarks.ContainsKey(code))
                            {
                                allRemarks[code] = kvp.Value;
                            }
                        }
                    }
                }

                var regularity = EnrollmentRules.DetermineRegularity(allRemarks);
                var isIrregular = regularity.Equals("Irregular", StringComparison.OrdinalIgnoreCase);

                // Update current request
                currentRequest.Type = isIrregular ? "Sophomore-Irregular" : "Sophomore-Regular";
                currentRequest.Status = "Enrolled";
                currentRequest.LastUpdatedAt = DateTime.UtcNow;

                currentRequest.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                currentRequest.ExtraFields["Academic.YearLevel"] = "2nd Year";
                currentRequest.ExtraFields["Academic.Semester"] = "2nd Semester";
                currentRequest.ExtraFields["Academic.AcademicYear"] = settings.AcademicYear ?? "";
                currentRequest.ExtraFields["Academic.Program"] = program;
                currentRequest.ExtraFields["EnrolledSubjects"] = string.Join(",", eligibleSubjects);
                currentRequest.ExtraFields["EnrollmentType"] = regularity;
                currentRequest.ExtraFields["SectionId"] = customSection.Id;

                if (preservedFlags != null)
                {
                    currentRequest.DocumentFlags = new Dictionary<string, string>(preservedFlags, StringComparer.OrdinalIgnoreCase);
                }

                await _db.UpdateEnrollmentRequestAsync(currentRequest);

                // Update student.Type
                try
                {
                    student.Type = currentRequest.Type;
                    await _db.UpdateStudentAsync(student);
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Updated student.Type = {currentRequest.Type}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Warning: failed to update student: {ex.Message}");
                }

                // Send email with PDF
                byte[]? pdfBytes = null;
                try
                {
                    pdfBytes = await _pdf.GenerateForRequestAsync(currentRequest.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] PDF generation failed: {ex.Message}");
                }

                try
                {
                    await _email.SendAcceptanceEmailAsync(currentRequest.Email, student.Username, null!, pdfBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] Email failed: {ex.Message}");
                }

                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] ✅ Successfully enrolled in 2nd Year 2nd Semester");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnrollRequest2ndYear2ndSemAsync] ❌ Error: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> EnrollRequest2ndYearAsync(EnrollmentRequest currentRequest)
        {// At the start of the method (after line 1950 in EnrollRequest2ndYearAsync)
            Console.WriteLine($"[EnrollRequest2ndYearAsync] ═══ DEBUG START ═══");
            Console.WriteLine($"[EnrollRequest2ndYearAsync] Request ID: {currentRequest.Id}");
            Console.WriteLine($"[EnrollRequest2ndYearAsync] Email: {currentRequest.Email}");

            if (currentRequest.ExtraFields != null)
            {
                Console.WriteLine($"[EnrollRequest2ndYearAsync] ExtraFields count: {currentRequest.ExtraFields.Count}");

                foreach (var kvp in currentRequest.ExtraFields)
                {
                    if (kvp.Key.Contains("Subject", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Contains("Enrolled", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Contains("Retake", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"[EnrollRequest2ndYearAsync] ❌ ExtraFields is NULL!");
            }

            Console.WriteLine($"[EnrollRequest2ndYearAsync] ═══ DEBUG END ═══");

            try
            {
                Console.WriteLine($"[EnrollRequest2ndYearAsync] Starting 2nd Year enrollment for {currentRequest.Email}");

                var student = await _db.GetStudentByEmailAsync(currentRequest.Email);
                if (student == null)
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Student not found");
                    return false;
                }

                var program = GetProgramForRequest(currentRequest);
                var settings = await _db.GetEnrollmentSettingsAsync();

                var currentYearLevel = currentRequest.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year");
                var currentSemester = currentRequest.ExtraFields?.GetValueOrDefault("Academic.Semester", "2nd Semester");

                var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(currentRequest, _db);
                if (allRemarks != null && allRemarks.Any())
                {
                    // Ensure current request carries the remarks (so the update persists before archiving/deleting old requests)
                    currentRequest.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in allRemarks)
                    {
                        // store as SubjectRemarks.<CODE> (no semester prefix because this is combined 1st-year data)
                        var key = $"SubjectRemarks.{kv.Key}";
                        currentRequest.ExtraFields[key] = kv.Value;
                    }

                    // Save authoritative copy to MongoDB (optional but recommended)
                    try
                    {
                        // student must exist (we checked earlier), save everything as 1st Year remarks
                        await _db.SaveStudentSubjectRemarksAsync(
                            student.Username,
                            allRemarks,
                            program,
                            semester: "1st Semester",    // conservative / canonical label (helper reads archives too)
                            yearLevel: "1st Year"
                        );
                        Console.WriteLine($"[EnrollRequest2ndYearAsync] Saved {allRemarks.Count} first-year remarks to student_subject_remarks for {student.Username}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EnrollRequest2ndYearAsync] Warning: failed to persist remarks to MongoDB: {ex.Message}");
                    }
                }
                var regularity = EnrollmentRules.DetermineRegularity(allRemarks);
                var isIrregular = regularity.Equals("Irregular", StringComparison.OrdinalIgnoreCase);

                // ✅ UPDATED: Get subjects from student's selection (includes retakes)
                var eligibleSubjects = new List<string>();

                // Priority 1: Use student-selected subjects from EnrolledSubjects
                // ✅ NEW: Check both field names
                if (currentRequest.ExtraFields != null)
                {
                    string? enrolledCsv = null;

                    // Try "EnrolledSubjects" first (used by irregular enrollment)
                    if (!currentRequest.ExtraFields.TryGetValue("EnrolledSubjects", out enrolledCsv) ||
                        string.IsNullOrWhiteSpace(enrolledCsv))
                    {
                        // Fallback to "SelectedSubjects" (used by 2nd year enrollment)
                        currentRequest.ExtraFields.TryGetValue("SelectedSubjects", out enrolledCsv);
                    }

                    if (!string.IsNullOrWhiteSpace(enrolledCsv))
                    {
                        eligibleSubjects.AddRange(enrolledCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                        Console.WriteLine($"[EnrollRequest2ndYearAsync] Using student-selected subjects: {string.Join(", ", eligibleSubjects)}");
                    }
                }
                // Priority 2: Fallback to eligibility map
                else if (currentRequest.SecondSemesterEligibility != null && currentRequest.SecondSemesterEligibility.Any())
                {
                    foreach (var kvp in currentRequest.SecondSemesterEligibility)
                    {
                        if (kvp.Value != null && kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        {
                            eligibleSubjects.Add(kvp.Key);
                        }
                    }
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Using eligibility map subjects: {string.Join(", ", eligibleSubjects)}");
                }

                // Validation: Ensure we have subjects to assign
                if (!eligibleSubjects.Any())
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] No eligible subjects found");
                    return false;
                }

                Dictionary<string, string>? preservedFlags = null;
                if (currentRequest.DocumentFlags != null && currentRequest.DocumentFlags.Any())
                {
                    preservedFlags = new Dictionary<string, string>(currentRequest.DocumentFlags, StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Preserved {preservedFlags.Count} document flag(s)");
                }

                await _db.ClearStudentScheduleAsync(student.Username);

                var sectionName = $"{program}-2ndYear-{student.Username}-{DateTime.UtcNow.Year}";
                var customSection = new CourseSection
                {
                    Id = $"{program}-{DateTime.UtcNow.Year}-{student.Username}-2ndYear",
                    Program = program,
                    Name = sectionName,
                    Capacity = 1,
                    CurrentCount = 0,
                    Year = DateTime.UtcNow.Year
                };

                await _db.CreateCustomSectionAsync(customSection);
                await _db.GenerateSectionScheduleAsync(customSection.Id, eligibleSubjects);
                await _db.EnrollStudentInSectionAsync(student.Username, customSection.Id);

                var schedule = await _db.GetStudentScheduleAsync(student.Username);
                if (schedule == null || !schedule.Any())
                {
                    await _db.DeleteSectionAsync(customSection.Id);
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Failed to generate schedule, deleted section {customSection.Id}");
                    return false;
                }

                Console.WriteLine($"[EnrollRequest2ndYearAsync] 🔍 Archiving previous enrolled records for {currentRequest.Email}");
                try
                {
                    var allRequests = await _db.GetRequestsByEmailAsync(currentRequest.Email);

                    if (allRequests != null && allRequests.Any())
                    {
                        int archivedCount = 0;
                        foreach (var oldRequest in allRequests)
                        {
                            if (oldRequest.Id == currentRequest.Id) continue;

                            var isEnrolled = oldRequest.Status != null &&
                                             oldRequest.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase);

                            if (!isEnrolled) continue;

                            var oldSem = oldRequest.ExtraFields?.GetValueOrDefault("Academic.Semester", "");
                            var oldYear = oldRequest.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");

                            Console.WriteLine($"[EnrollRequest2ndYearAsync] 📋 Checking: {oldRequest.Id}, Year={oldYear}, Sem={oldSem}");

                            if (oldYear.Contains("1st Year", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Archiving 1st year record {oldRequest.Id}");
                                await _db.ArchiveEnrollmentRequestAsync(oldRequest, "Advanced to 2nd Year 1st Semester");
                                await _db.DeleteEnrollmentRequestAsync(oldRequest.Id);
                                archivedCount++;
                                Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Archived and deleted {oldRequest.Id}");
                            }
                        }

                        Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Archived {archivedCount} record(s)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] ⚠️ Archiving failed: {ex.Message}");
                }

                currentRequest.Program = program;
                currentRequest.Type = isIrregular ? "Sophomore-Irregular" : "Sophomore-Regular";
                currentRequest.Status = "Enrolled";
                currentRequest.LastUpdatedAt = DateTime.UtcNow;

                currentRequest.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                currentRequest.ExtraFields["Academic.YearLevel"] = "2nd Year";
                currentRequest.ExtraFields["Academic.Semester"] = "1st Semester";
                currentRequest.ExtraFields["Academic.AcademicYear"] = settings.AcademicYear ?? "";
                currentRequest.ExtraFields["Academic.Program"] = program;
                currentRequest.ExtraFields["EnrolledSubjects"] = string.Join(",", eligibleSubjects);
                currentRequest.ExtraFields["EnrollmentType"] = regularity;
                currentRequest.ExtraFields["AdvancedFrom"] = $"{currentYearLevel} {currentSemester}";
                currentRequest.ExtraFields["SectionId"] = customSection.Id;

                if (preservedFlags != null)
                {
                    currentRequest.DocumentFlags = new Dictionary<string, string>(preservedFlags, StringComparer.OrdinalIgnoreCase);
                }

                await _db.UpdateEnrollmentRequestAsync(currentRequest);
                Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Updated enrollment to 2nd Year: {currentRequest.Id}");

                // Update student.Type to reflect new level
                try
                {
                    student.Type = currentRequest.Type;
                    await _db.UpdateStudentAsync(student);
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Updated student.Type = {currentRequest.Type} for {student.Username}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Warning: failed to update student record: {ex.Message}");
                }

                var newRequest = new EnrollmentRequest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Email = currentRequest.Email,
                    FullName = currentRequest.FullName,
                    Program = program,
                    Type = isIrregular ? "Sophomore-Irregular" : "Sophomore-Regular",
                    Status = "Enrolled",
                    SubmittedAt = currentRequest.SubmittedAt,
                    LastUpdatedAt = DateTime.UtcNow,
                    EmergencyContactName = currentRequest.EmergencyContactName,
                    EmergencyContactPhone = currentRequest.EmergencyContactPhone,
                    DocumentFlags = preservedFlags != null ? new Dictionary<string, string>(preservedFlags, StringComparer.OrdinalIgnoreCase) : null,
                    ExtraFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                };
                if (currentRequest.ExtraFields != null)
                {
                    foreach (var kvp in currentRequest.ExtraFields)
                    {
                        // keep most fields; skip internal-only eligibility aggregation if you want
                        if (kvp.Key.StartsWith("SecondSemesterEligibility", StringComparison.OrdinalIgnoreCase))
                            continue;

                        newRequest.ExtraFields[kvp.Key] = kvp.Value;
                    }
                }

                // Ensure all extracted first-year remarks exist on the new request explicitly (no-op if already copied)
                if (allRemarks != null && allRemarks.Any())
                {
                    foreach (var kv in allRemarks)
                    {
                        var key = $"SubjectRemarks.{kv.Key}";
                        if (!newRequest.ExtraFields.ContainsKey(key))
                            newRequest.ExtraFields[key] = kv.Value;
                    }
                }

                newRequest.ExtraFields["Academic.YearLevel"] = "2nd Year";
                newRequest.ExtraFields["Academic.Semester"] = "1st Semester";
                newRequest.ExtraFields["Academic.AcademicYear"] = settings.AcademicYear ?? "";
                newRequest.ExtraFields["Academic.Program"] = program;
                newRequest.ExtraFields["EnrolledSubjects"] = string.Join(",", eligibleSubjects);
                newRequest.ExtraFields["EnrollmentType"] = regularity;
                newRequest.ExtraFields["AdvancedFrom"] = $"{currentYearLevel} {currentSemester}";
                newRequest.ExtraFields["SectionId"] = customSection.Id;

                await _db.CreateEnrollmentRequestAsync(newRequest);
                Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Created new 2nd Year enrollment record {newRequest.Id}");

                byte[]? pdfBytes = null;
                try
                {
                    pdfBytes = await _pdf.GenerateForRequestAsync(newRequest.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] PDF generation failed: {ex.Message}");
                }

                try
                {
                    await _email.SendAcceptanceEmailAsync(currentRequest.Email, student.Username, null!, pdfBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EnrollRequest2ndYearAsync] Email failed: {ex.Message}");
                }

                Console.WriteLine($"[EnrollRequest2ndYearAsync] ✅ Successfully completed 2nd Year enrollment");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnrollRequest2ndYearAsync] ❌ Error: {ex.Message}");
                return false;
            }
        }


        [HttpGet]
        public async Task<IActionResult> RegistrationSlip(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Invalid id.");

            var req = await _db.GetEnrollmentRequestByIdAsync(id);
            if (req is null)
                return BadRequest("Request not found.");

            var student = await _db.GetStudentByEmailAsync(req.Email);
            if (student is null)
                return BadRequest("Student not found for this request.");

            var sse = await _db.GetStudentSectionEnrollmentAsync(student.Username);
            if (sse is null)
                return BadRequest("Section enrollment not found.");

            var section = await _db.GetSectionByIdAsync(sse.SectionId);
            var meetings = (await _db.GetStudentScheduleAsync(student.Username)) ?? new List<ClassMeeting>();
            var roomNames = await _db.GetRoomNamesByIdsAsync(meetings.Select(m => m.RoomId));

            // Determine program/year/semester for correct subject lookup
            var program = GetProgramForRequest(req);
            var yearLevel = req.ExtraFields?.TryGetValue("Academic.YearLevel", out var yl) == true && !string.IsNullOrWhiteSpace(yl) ? yl : "1st Year";
            var semester = req.ExtraFields?.TryGetValue("Academic.Semester", out var sem) == true && !string.IsNullOrWhiteSpace(sem) ? sem : "1st Semester";

            // Build subject dictionary from the correct year/semester
            var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var subjectList = GetSubjectsForYearAndSemester(program, yearLevel, semester);
                foreach (var s in subjectList)
                    if (!string.IsNullOrWhiteSpace(s.Code))
                        subjectDict[s.Code] = (s.Title, s.Units);

                // Fallback: if a code is still missing, search across all years for the program
                if (!subjectDict.Any())
                {
                    var allYears = GetSubjectsForYearAndSemester(program, "1st Year", "1st Semester")
                        .Concat(GetSubjectsForYearAndSemester(program, "1st Year", "2nd Semester"));
                    foreach (var s in allYears)
                        if (!string.IsNullOrWhiteSpace(s.Code))
                            subjectDict[s.Code] = (s.Title, s.Units);
                }
            }
            catch { /* defensively continue */ }

            static string DayName(int d) => d switch
            {
                0 => "Sun",
                1 => "Mon",
                2 => "Tue",
                3 => "Wed",
                4 => "Thu",
                5 => "Fri",
                6 => "Sat",
                _ => "Day"
            };

            // ✅ Get retake subjects from ExtraFields
            var retakeSubjectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (req.ExtraFields != null && req.ExtraFields.TryGetValue("RetakeSubjects", out var retakeCsv) && !string.IsNullOrWhiteSpace(retakeCsv))
            {
                foreach (var code in retakeCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    retakeSubjectCodes.Add(code.Trim());
                }
            }


            var subjects = new List<AdminRegistrationSlipSubject>();
            foreach (var m in meetings)
            {
                var code = m.CourseCode ?? "";
                subjectDict.TryGetValue(code, out var meta);

                // ✅ Mark if this is a retake subject
                var isRetake = retakeSubjectCodes.Contains(code);

                subjects.Add(new AdminRegistrationSlipSubject
                {
                    Code = code,
                    Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                    Units = meta.Units,
                    Room = roomNames.TryGetValue(m.RoomId ?? "", out var rn) ? rn : (m.RoomId ?? ""),
                    Schedule = $"{DayName(m.DayOfWeek)} {(string.IsNullOrWhiteSpace(m.DisplayTime) ? "" : m.DisplayTime)}".Trim(),
                    IsRetake = isRetake // ✅ NEW: Mark retake subjects
                });
            }

            var extra = req.ExtraFields ?? new Dictionary<string, string>();
            string G(string p, string n)
            {
                var key = string.IsNullOrEmpty(p) ? n : $"{p}.{n}";
                return extra.TryGetValue(key, out var v) ? v : "";
            }

            var vm = new AdminRegistrationSlipViewModel
            {
                Program = section?.Program ?? (req.Program ?? ""),
                YearLevel = G("Academic", "YearLevel"),
                Semester = G("Academic", "Semester"),
                SectionName = section?.Name ?? "",
                Regularity = (req.Type != null && (req.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase) || string.Equals(req.Type, "Transferee", StringComparison.OrdinalIgnoreCase))) ? "Irregular" : "Regular",
                GraduatingStatus = "Not Graduating",
                LastName = G("Student", "LastName"),
                FirstName = G("Student", "FirstName"),
                MiddleName = G("Student", "MiddleName"),
                StudentNumber = student.Username ?? "",
                DateEnrolledUtc = sse.EnrolledAt,
                Subjects = subjects,
                DeanName = "Engr. Juan Dela Cruz",
                RequestId = req.Id
            };

            return PartialView("_RegistrationSlipModal", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hold(string id, string reason, [FromForm] Dictionary<string, string>? flags, [FromForm] bool reviewed = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }
            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Enrollment request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (!reviewed)
            {
                TempData["Error"] = "Please open Review and confirm you have reviewed the request.";
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            var isSecondSem = IsSecondSemRequest(request);
            var isTransferee = !string.IsNullOrWhiteSpace(request.Type) && request.Type.Equals("Transferee", StringComparison.OrdinalIgnoreCase);

            if (!isSecondSem || isTransferee)
            {
                MergePostedFlags(request, flags);
                if (!AreFlagsComplete(request))
                {
                    TempData["Error"] = "Flag all required documents before putting the request on hold.";
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
            }

            // For 2nd-sem requests allow On Hold only when either:
            // - Program capacity is reached OR
            // - any required document other than GoodMoral/Diploma is "To be followed"
            if (isSecondSem)
            {
                var settings = await _db.GetEnrollmentSettingsAsync();
                var program = (request.Program ?? "").Trim();
                var capacityReached = false;
                if (!string.IsNullOrEmpty(program) &&
                    settings.ProgramCapacities != null &&
                    settings.ProgramCapacities.TryGetValue(program, out var cap) &&
                    cap > 0)
                {
                    var enrolled = await _db.CountEnrolledByProgramAsync(program);
                    capacityReached = enrolled >= cap;
                }

                var flagsDict = request.DocumentFlags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var toBeFollowedExists = flagsDict.Any(kv =>
                    !string.Equals(kv.Key, "GoodMoral", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(kv.Key, "Diploma", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((kv.Value ?? "").Trim(), "To be followed", StringComparison.OrdinalIgnoreCase)
                );

                if (!capacityReached && !toBeFollowedExists)
                {
                    TempData["Error"] = "For 2nd Sem Pending, On Hold is only allowed when Program capacity is reached or required documents (except Good Moral/Diploma) are marked 'To be followed'.";
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
            }

            if (request.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Cannot put an enrolled student on hold.";
                return RedirectToAction(nameof(Dashboard));
            }

            request.Status = "On Hold";
            request.Reason = string.IsNullOrWhiteSpace(reason) ? "Awaiting further review" : reason.Trim();
            request.LastUpdatedAt = DateTime.UtcNow;
            await _db.UpdateEnrollmentRequestAsync(request);

            // Notify admins for On Hold (manual or otherwise)
            try
            {
                var link = Url.Action("RequestDetails", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                {
                    type = "OnHoldPlaced",
                    title = "Request placed On Hold",
                    message = $"{request.FullName} • Reason: {request.Reason}.",
                    severity = "warning",
                    icon = "pause-circle",
                    id = request.Id,
                    link,
                    email = request.Email,
                    program = request.Program,
                    status = request.Status,
                    submittedAt = request.SubmittedAt
                });
            }
            catch { /* non-fatal */ }

            var flagsSummary = BuildFlagsSummary(request);
            try
            {
                await _email.SendOnHoldEmailDetailedAsync(request.Email, request.Reason ?? "On Hold", flagsSummary);
                TempData["Info"] = "Request placed on hold. Notification email sent.";
            }
            catch
            {
                TempData["Error"] = "Request placed on hold, but sending email failed.";
            }
            return RedirectToListFor(request);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(string id, string reason, string? notes, [FromForm] Dictionary<string, string>? flags)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }
            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Enrollment request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Merge posted flags first
            MergePostedFlags(request, flags);

            if (!AreFlagsComplete(request))
            {
                TempData["Error"] = "2nd semester Rejection WIP || Flag all required documents before rejecting the request.";
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            // For 2nd-sem requests allow Reject only when at least one required doc is Ineligible
            if (IsSecondSemRequest(request))
            {
                var flagsDict = request.DocumentFlags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var anyIneligible = flagsDict.Values.Any(v => string.Equals((v ?? "").Trim(), "Ineligible", StringComparison.OrdinalIgnoreCase));
                if (!anyIneligible)
                {
                    TempData["Error"] = "Reject is only allowed for 2nd Sem Pending when one or more documents are marked Ineligible.";
                    return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
                }
            }

            if (request.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Cannot reject an enrolled student.";
                return RedirectToAction(nameof(Dashboard));
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Unqualified", "Violates Policy", "Missed Deadlines", "No Capacity" };
            if (!allowed.Contains((reason ?? "").Trim()))
            {
                TempData["Error"] = "Invalid rejection reason.";
                return RedirectToAction(nameof(Dashboard));
            }

            request.Status = "Rejected";
            request.Reason = reason.Trim();
            request.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            request.LastUpdatedAt = DateTime.UtcNow;
            request.EditToken = null;
            request.EditTokenExpires = null;

            await _db.UpdateEnrollmentRequestAsync(request);
            var flagsSummary = BuildFlagsSummary(request);

            try
            {
                await _email.SendRejectionEmailDetailedAsync(request.Email, request.Reason, flagsSummary, request.Notes, editLink: null);
                TempData["Info"] = "Request rejected. Email with details sent.";
            }
            catch
            {
                TempData["Error"] = "Request rejected, but sending email failed.";
            }

            return RedirectToListFor(request);
        }
       
        [HttpGet]
        public async Task<IActionResult> EnrolledDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id. but accepted 3, no slip";
                return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });
            }

            var req = await _db.GetEnrollmentRequestByIdAsync(id);
            if (req is null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });
            }

            // Verify this is an enrolled record
            if (string.IsNullOrWhiteSpace(req.Status) ||
                !req.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "This record is not enrolled. Use Request Details instead.";
                return RedirectToAction(nameof(RequestDetails), new { id });
            }

            // Merge school data for 2nd semester requests
            req = await MergeSchoolDataFrom1stSemesterAsync(req);

            var programForReq = GetProgramForRequest(req);
            ViewBag.ProgramNormalized = programForReq;

            var enrolledSemester = req.ExtraFields?.TryGetValue("Academic.Semester", out var sem) == true
                ? sem
                : "1st Semester";

            var enrolledYearLevel = req.ExtraFields?.TryGetValue("Academic.YearLevel", out var yl) == true
                ? yl
                : "1st Year";

            var is1stSemester = enrolledSemester.StartsWith("1", StringComparison.OrdinalIgnoreCase);
            var is2ndSemester = enrolledSemester.StartsWith("2", StringComparison.OrdinalIgnoreCase);
            var is1stYear = enrolledYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase);
            var is2ndYear = enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"[EnrolledDetails] Student: {req.FullName}, Year: {enrolledYearLevel}, Semester: {enrolledSemester}");
            Console.WriteLine($"[EnrolledDetails] Flags: is1stYear={is1stYear}, is2ndYear={is2ndYear}, is1stSemester={is1stSemester}");

            // ✅ Get existing remarks from MongoDB + ExtraFields
            var existingRemarks = await GetExistingSubjectRemarksForEnrolledAsync(req);
            ViewBag.ExistingSubjectRemarks = existingRemarks;

            // ✅ Get prerequisites for the correct year/semester
            ViewBag.Prerequisites = GetPrerequisiteInfo(programForReq, enrolledYearLevel, enrolledSemester);

            // ✅ Build subject dictionary for metadata lookup (covers both 1st and 2nd year)
            var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

            if (is1stYear)
            {
                if (string.Equals(programForReq, "BSENT", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                    foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                }
                else
                {
                    foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                    foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                }
            }
            else if (is2ndYear)
            {
                // ✅ Load 2nd Year subject metadata
                if (string.Equals(programForReq, "BSENT", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                    foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                }
                else
                {
                    foreach (var s in E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                    foreach (var s in E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                        subjectDict[s.Code] = (s.Title, s.Units);
                }
            }

            var student = await _db.GetStudentByEmailAsync(req.Email);

            // ✅ Get ACTUAL assigned subjects from student schedule
            var subjectsForRemarks = new List<SubjectRow>();

            if (student != null && !string.IsNullOrWhiteSpace(student.Username))
            {
                var actualSchedule = await _db.GetStudentScheduleAsync(student.Username);
                if (actualSchedule != null && actualSchedule.Any())
                {
                    var roomNames = await _db.GetRoomNamesByIdsAsync(actualSchedule.Select(m => m.RoomId));
                    var currentSchedule = new List<dynamic>();

                    Console.WriteLine($"[EnrolledDetails] Loading {actualSchedule.Count} subjects from schedule for {student.Username}");

                    // Build subjectsForRemarks from ACTUAL schedule
                    foreach (var schedule in actualSchedule)
                    {
                        var code = schedule.CourseCode ?? "";
                        if (string.IsNullOrWhiteSpace(code)) continue;

                        subjectDict.TryGetValue(code, out var meta);

                        // Add to subjects list for remarks
                        subjectsForRemarks.Add(new SubjectRow
                        {
                            Code = code,
                            Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                            Units = meta.Units,
                            PreRequisite = ""
                        });

                        currentSchedule.Add(new
                        {
                            Code = code,
                            Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                            Units = meta.Units,
                            Day = schedule.DayOfWeek switch
                            {
                                1 => "Mon",
                                2 => "Tue",
                                3 => "Wed",
                                4 => "Thu",
                                5 => "Fri",
                                6 => "Sat",
                                7 => "Sun",
                                _ => "-"
                            },
                            Time = schedule.DisplayTime ?? "",
                            Room = roomNames.TryGetValue(schedule.RoomId ?? "", out var rn) ? rn : (schedule.RoomId ?? "")
                        });
                    }

                    ViewBag.CurrentSchedule = currentSchedule;
                    ViewBag.HasCurrentSchedule = true;
                }
                else
                {
                    // Fallback: Use canonical subjects for the correct year/semester
                    Console.WriteLine($"[EnrolledDetails] No schedule found for {student.Username}, using canonical subjects");
                    subjectsForRemarks = GetSubjectsForYearAndSemester(programForReq, enrolledYearLevel, enrolledSemester);
                }
            }
            else
            {
                // No student account, use canonical subjects
                Console.WriteLine($"[EnrolledDetails] No student account for {req.Email}, using canonical subjects");
                subjectsForRemarks = GetSubjectsForYearAndSemester(programForReq, enrolledYearLevel, enrolledSemester);
            }

            // ✅ Map subjects to ViewBag for the view (used by the remarks table)
            var subjects = new List<dynamic>();
            foreach (var subject in subjectsForRemarks)
            {
                subjects.Add(new
                {
                    Code = subject.Code,
                    Title = subject.Title,
                    Units = subject.Units
                });
            }
            ViewBag.SubjectSchedules = subjects;

            // ✅ Pass existing eligibility if available
            if (req.SecondSemesterEligibility != null && req.SecondSemesterEligibility.Any())
            {
                ViewBag.ExistingSecondSemEligibility = req.SecondSemesterEligibility;
            }

            var settings = await _db.GetEnrollmentSettingsAsync();
            ViewBag.EnrollmentSettings = settings;

            // ✅ Pass year/semester info to the view
            ViewBag.EnrolledSemester = enrolledSemester;
            ViewBag.EnrolledYearLevel = enrolledYearLevel;
            ViewBag.Is1stSemester = is1stSemester;
            ViewBag.Is2ndSemester = is2ndSemester;

            ViewData["Title"] = "Enrolled Details";
            Console.WriteLine($"[EnrolledDetails] Rendering view for {enrolledYearLevel} {enrolledSemester} student");

            // ✅ NO REDIRECT - Always use EnrolledDetails.cshtml
            return View("~/Areas/Admin/Views/Admin/EnrolledDetails.cshtml", req);
        }


        [HttpGet]
        public async Task<IActionResult> Enrolled2ndYearDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid enrollment ID.";
                return RedirectToAction(nameof(Enrolled));
            }

            var enrollment = await _db.GetEnrollmentRequestByIdAsync(id);
            if (enrollment == null)
            {
                TempData["Error"] = "Enrollment record not found.";
                return RedirectToAction(nameof(Enrolled));
            }

            var program = GetProgramForRequest(enrollment);
            ViewBag.ProgramNormalized = program;
            var academicYear = enrollment.ExtraFields?.GetValueOrDefault("Academic.AcademicYear", "");

            // ✅ NEW: Load archived 1st Year remarks
            var existingRemarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get all archived 1st Year records for this student
                var archives = await _db.GetArchivesAsync(academicYear);
                var studentArchives = archives
                    .Where(a => a.Email.Equals(enrollment.Email, StringComparison.OrdinalIgnoreCase) &&
                               a.ExtraFields != null &&
                               a.ExtraFields.GetValueOrDefault("Academic.YearLevel", "").Contains("1st Year"))
                    .ToList();

                Console.WriteLine($"[Enrolled2ndYearDetails] Found {studentArchives.Count} archived 1st year records for {enrollment.Email}");

                // Extract ALL subject remarks from archived 1st Year records
                foreach (var archive in studentArchives)
                {
                    if (archive.ExtraFields == null) continue;

                    foreach (var kvp in archive.ExtraFields)
                    {
                        if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                        {
                            var code = kvp.Key.Replace("SubjectRemarks.", "")
                                             .Replace("2ndSem.", "");

                            // Only add if not already present (keep most recent)
                            if (!existingRemarks.ContainsKey(code))
                            {
                                existingRemarks[code] = kvp.Value;
                                Console.WriteLine($"  - Loaded remark: {code} = {kvp.Value}");
                            }
                        }
                    }
                }

                Console.WriteLine($"[Enrolled2ndYearDetails] Total remarks loaded: {existingRemarks.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Enrolled2ndYearDetails] Error loading archived remarks: {ex.Message}");
            }

            ViewBag.ExistingSubjectRemarks = existingRemarks;
            var yearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "2nd Year");
            var semester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester");

            Console.WriteLine($"[Enrolled2ndYearDetails] Viewing enrolled 2nd Year student: {yearLevel} {semester}");

            // Get 2nd Year 1st Semester subjects
            var secondYearSubjects = GetSubjectsForYearAndSemester(program, yearLevel, semester);
            ViewBag.SecondYearSubjects = secondYearSubjects;

            // Get student's enrolled subjects
            var enrolledSubjectCodes = enrollment.ExtraFields?.GetValueOrDefault("EnrolledSubjects", "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToList() ?? new List<string>();

            ViewBag.EnrolledSubjects = enrolledSubjectCodes;

            // Get student's schedule
            var student = await _db.GetStudentByEmailAsync(enrollment.Email);
            if (student != null)
            {
                var schedule = await _db.GetStudentScheduleAsync(student.Username);
                if (schedule != null && schedule.Any())
                {
                    var roomNames = await _db.GetRoomNamesByIdsAsync(schedule.Select(m => m.RoomId));
                    var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

                    // Build subject metadata lookup
                    if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }
                    else
                    {
                        foreach (var s in E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }

                    var scheduleDetails = new List<dynamic>();
                    foreach (var meeting in schedule)
                    {
                        var code = meeting.CourseCode ?? "";
                        subjectDict.TryGetValue(code, out var meta);

                        scheduleDetails.Add(new
                        {
                            Code = code,
                            Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                            Units = meta.Units,
                            Day = meeting.DayOfWeek switch
                            {
                                1 => "Mon",
                                2 => "Tue",
                                3 => "Wed",
                                4 => "Thu",
                                5 => "Fri",
                                6 => "Sat",
                                7 => "Sun",
                                _ => "-"
                            },
                            Time = meeting.DisplayTime ?? "",
                            Room = roomNames.TryGetValue(meeting.RoomId ?? "", out var rn) ? rn : (meeting.RoomId ?? "")
                        });
                    }

                    ViewBag.CurrentSchedule = scheduleDetails;
                }
            }

            // Get prerequisite map for 3rd Year (for future progression)
            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);
            ViewBag.PrerequisiteMap = prereqMap;

            var settings = await _db.GetEnrollmentSettingsAsync();
            ViewBag.EnrollmentSettings = settings;

            ViewBag.EnrolledSemester = semester;
            ViewBag.EnrolledYearLevel = yearLevel;

            ViewData["Title"] = "2nd Year Enrolled Details";
            return View("~/Areas/Admin/Views/Admin/Enrolled2ndYearDetails.cshtml", enrollment);
        }

        private async Task<Dictionary<string, string>> GetExistingSubjectRemarksForEnrolledAsync(EnrollmentRequest request)
        {
            var remarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Step 1: Load from MongoDB (authoritative for saved remarks)
            var student = await _db.GetStudentByEmailAsync(request.Email);
            if (student != null && !string.IsNullOrWhiteSpace(student.Username))
            {
                var mongoRemarks = await _db.GetStudentSubjectRemarksAsync(student.Username);
                if (mongoRemarks != null && mongoRemarks.Any())
                {
                    foreach (var remark in mongoRemarks)
                    {
                        if (!string.IsNullOrWhiteSpace(remark.SubjectCode))
                        {
                            remarks[remark.SubjectCode] = remark.Remark ?? "ongoing";
                        }
                    }

                    Console.WriteLine($"[GetExistingSubjectRemarksForEnrolledAsync] Loaded {remarks.Count} remarks from MongoDB for {student.Username}");
                }
            }

            // Step 2: Load from current request ExtraFields
            if (request.ExtraFields != null)
            {
                foreach (var kvp in request.ExtraFields)
                {
                    if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    {
                        var subjectCode = kvp.Key.Substring("SubjectRemarks.".Length)
                                                 .Replace("2ndSem.", "");

                        if (!remarks.ContainsKey(subjectCode))
                        {
                            remarks[subjectCode] = kvp.Value;
                        }
                    }
                }
            }

            // ✅ NEW: Step 3: Load from archived records (critical for 2nd year students)
            var academicYear = request.ExtraFields?.GetValueOrDefault("Academic.AcademicYear", "");
            var enrolledYearLevel = request.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");

            // If this is a 2nd year student, load ALL archived 1st year records
            if (enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[GetExistingSubjectRemarksForEnrolledAsync] Loading archived 1st year remarks for 2nd year student");

                try
                {
                    var archives = await _db.GetArchivesAsync(academicYear);
                    var studentArchives = archives
                        .Where(a => a.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
                                   a.ExtraFields != null &&
                                   a.ExtraFields.GetValueOrDefault("Academic.YearLevel", "").Contains("1st Year"))
                        .ToList();

                    Console.WriteLine($"[GetExistingSubjectRemarksForEnrolledAsync] Found {studentArchives.Count} archived 1st year records");

                    foreach (var archive in studentArchives)
                    {
                        if (archive.ExtraFields == null) continue;

                        foreach (var kvp in archive.ExtraFields)
                        {
                            if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                            {
                                var code = kvp.Key.Replace("SubjectRemarks.", "")
                                                 .Replace("2ndSem.", "");

                                if (!remarks.ContainsKey(code))
                                {
                                    remarks[code] = kvp.Value;
                                    Console.WriteLine($"  - Loaded archived remark: {code} = {kvp.Value}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GetExistingSubjectRemarksForEnrolledAsync] Error loading archived remarks: {ex.Message}");
                }
            }

            Console.WriteLine($"[GetExistingSubjectRemarksForEnrolledAsync] Total remarks after all sources: {remarks.Count}");
            return remarks;
        }

        [HttpGet]
        public async Task<IActionResult> DebugArchives()
        {
            try
            {
                // Get ALL archives without any filter
                var allArchives = await _db.GetArchivesAsync(academicYear: null, take: 1000);

                var diagnostics = new
                {
                    TotalCount = allArchives.Count,
                    Records = allArchives.Select(a => new
                    {
                        a.Id,
                        a.FullName,
                        a.Email,
                        a.AcademicYear,
                        a.Semester,
                        a.StatusAtArchive,
                        a.ArchiveReason,
                        a.ArchivedAt,
                        HasExtraFields = a.ExtraFields != null && a.ExtraFields.Any(),
                        ExtraFieldsCount = a.ExtraFields?.Count ?? 0,
                        SemesterInExtraFields = a.ExtraFields?.GetValueOrDefault("Academic.Semester", "NOT FOUND")
                    }).ToList(),

                    // Group by semester to see distribution
                    BySemester = allArchives
                        .GroupBy(a => string.IsNullOrWhiteSpace(a.Semester) ? "EMPTY" : a.Semester)
                        .Select(g => new { Semester = g.Key, Count = g.Count() })
                        .ToList(),

                    // Group by academic year
                    ByAcademicYear = allArchives
                        .GroupBy(a => string.IsNullOrWhiteSpace(a.AcademicYear) ? "EMPTY" : a.AcademicYear)
                        .Select(g => new { AcademicYear = g.Key, Count = g.Count() })
                        .ToList()
                };

                return Ok(diagnostics);
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message, stack = ex.StackTrace });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateEligibility(string id, [FromForm] Dictionary<string, string>? subjectRemarks)
        {
            try
            {
                Console.WriteLine($"[CalculateEligibility] Request received for ID: {id}");
                Console.WriteLine($"[CalculateEligibility] Subject remarks count: {subjectRemarks?.Count ?? 0}");

                if (string.IsNullOrWhiteSpace(id))
                {
                    Console.WriteLine("[CalculateEligibility] Invalid request: missing ID");
                    return Json(new { error = "Invalid request: missing enrollment ID" });
                }

                if (subjectRemarks == null || !subjectRemarks.Any())
                {
                    Console.WriteLine("[CalculateEligibility] Invalid request: no subject remarks");
                    return Json(new { error = "No subject remarks provided" });
                }

                var request = await _db.GetEnrollmentRequestByIdAsync(id);
                if (request == null)
                {
                    Console.WriteLine($"[CalculateEligibility] Enrollment request not found: {id}");
                    return Json(new { error = "Enrollment request not found" });
                }

                // Get program and year/semester info
                var program = GetProgramForRequest(request);
                var enrolledYearLevel = request.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year") ?? "1st Year";
                var enrolledSemester = request.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester") ?? "1st Semester";

                var is1stYear = enrolledYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase);
                var is2ndYear = enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase);
                var is1stSemester = enrolledSemester.StartsWith("1", StringComparison.OrdinalIgnoreCase);

                Console.WriteLine($"[CalculateEligibility] Program: {program}, Year: {enrolledYearLevel}, Semester: {enrolledSemester}");

                // ✅ Determine target subjects based on enrolled year/semester
                List<SubjectRow> targetSubjects;
                string targetYearLevel;
                string targetSemester;

                if (is1stYear && is1stSemester)
                {
                    // 1st Year 1st Sem → Calculate for 1st Year 2nd Sem
                    targetSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                        ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList()
                        : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
                    targetYearLevel = "1st Year";
                    targetSemester = "2nd Semester";
                }
                else if (is2ndYear && is1stSemester)
                {
                    // ✅ NEW: 2nd Year 1st Sem → Calculate for 2nd Year 2nd Sem
                    targetSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                        ? E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList()
                        : E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList();
                    targetYearLevel = "2nd Year";
                    targetSemester = "2nd Semester";
                }
                else
                {
                    return Json(new { error = "Eligibility calculation is only available for 1st semester enrollments." });
                }

                Console.WriteLine($"[CalculateEligibility] Evaluating {targetSubjects.Count} target subjects for {targetYearLevel} {targetSemester}");

                // ✅ For 2nd year students, include ALL previous remarks (1st year + current 2nd year 1st sem)
                var allRemarks = new Dictionary<string, string>(subjectRemarks, StringComparer.OrdinalIgnoreCase);

                if (is2ndYear)
                {
                    Console.WriteLine($"[CalculateEligibility] Loading 1st year remarks for 2nd year student");

                    // Get all existing remarks (includes archived data)
                    var existingRemarks = await GetExistingSubjectRemarksForEnrolledAsync(request);

                    foreach (var kvp in existingRemarks)
                    {
                        // Don't overwrite current semester remarks
                        if (!allRemarks.ContainsKey(kvp.Key))
                        {
                            allRemarks[kvp.Key] = kvp.Value;
                            Console.WriteLine($"  - Added 1st year remark: {kvp.Key} = {kvp.Value}");
                        }
                    }

                    Console.WriteLine($"[CalculateEligibility] Total remarks (current + 1st year): {allRemarks.Count}");
                }

                // Calculate eligibility
                var eligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var subject in targetSubjects)
                {
                    var code = subject.Code;
                    var prereqs = subject.PreRequisite;

                    if (string.IsNullOrWhiteSpace(prereqs))
                    {
                        eligibility[code] = "Can enroll - No prerequisites";
                        Console.WriteLine($"[CalculateEligibility] {code}: No prerequisites");
                        continue;
                    }

                    // Parse prerequisites
                    var prereqList = prereqs.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();

                    // Check if all prerequisites are passed
                    var failedPrereqs = new List<string>();
                    var ongoingPrereqs = new List<string>();

                    foreach (var prereq in prereqList)
                    {
                        if (!allRemarks.TryGetValue(prereq, out var remark))
                        {
                            Console.WriteLine($"[CalculateEligibility] {code}: Prerequisite {prereq} not found in remarks");
                            ongoingPrereqs.Add(prereq);
                        }
                        else if (remark.Equals("fail", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[CalculateEligibility] {code}: Prerequisite {prereq} FAILED");
                            failedPrereqs.Add(prereq);
                        }
                        else if (remark.Equals("ongoing", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[CalculateEligibility] {code}: Prerequisite {prereq} ONGOING");
                            ongoingPrereqs.Add(prereq);
                        }
                        else
                        {
                            Console.WriteLine($"[CalculateEligibility] {code}: Prerequisite {prereq} PASSED");
                        }
                    }

                    // Determine eligibility
                    if (failedPrereqs.Any())
                    {
                        eligibility[code] = $"Cannot enroll - Failed prerequisites: {string.Join(", ", failedPrereqs)}";
                    }
                    else if (ongoingPrereqs.Any())
                    {
                        eligibility[code] = $"Cannot enroll - Prerequisites not met: {string.Join(", ", ongoingPrereqs)}";
                    }
                    else
                    {
                        eligibility[code] = "Can enroll - All prerequisites passed";
                    }

                    Console.WriteLine($"[CalculateEligibility] {code}: {eligibility[code]}");
                }

                Console.WriteLine($"[CalculateEligibility] Calculation complete, returning {eligibility.Count} results");

                return Json(new { eligibility });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CalculateEligibility] ERROR: {ex.Message}");
                Console.WriteLine($"[CalculateEligibility] Stack trace: {ex.StackTrace}");
                return Json(new { error = $"Calculation failed: {ex.Message}" });
            }
        }


        [HttpPost]
        public async Task<IActionResult> CalculateSecondYearEligibility(
    string id,
    [FromForm] Dictionary<string, string>? subjectRemarks)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { success = false, message = "Invalid request id." });

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
                return NotFound(new { success = false, message = "Enrollment request not found." });

            var currentYearLevel = request.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");
            var currentSemester = request.ExtraFields?.GetValueOrDefault("Academic.Semester", "");

            if (!currentYearLevel.Contains("1st", StringComparison.OrdinalIgnoreCase) ||
                !currentSemester.Contains("2nd", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This calculation is only for students enrolled in 1st Year 2nd Semester."
                });
            }

            var settings = await _db.GetEnrollmentSettingsAsync();
            if (!EnrollmentRules.IsEligibleForNextYearEnrollment(request, settings))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Student is not yet eligible for 2nd Year enrollment. 2nd semester must be completed."
                });
            }

            request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var student = await _db.GetStudentByEmailAsync(request.Email);
            if (student == null)
                return BadRequest(new { success = false, message = "Student account not found." });

            var program = GetProgramForRequest(request);

            // ✅ CRITICAL FIX: Get ACTUAL enrolled subjects from student schedule
            var actualSchedule = await _db.GetStudentScheduleAsync(student.Username);
            var actualSubjectCodes = actualSchedule?
                .Select(m => m.CourseCode ?? "")
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"[CalculateSecondYearEligibility] Student {student.Username} has {actualSubjectCodes.Count} subjects: {string.Join(", ", actualSubjectCodes)}");

            // Mode 1: Manual Override (if subjectRemarks provided in POST)
            if (subjectRemarks != null && subjectRemarks.Any())
            {
                // ✅ CRITICAL FIX: Only validate subjects the student actually took
                var enrolledSubjects = new List<SubjectRow>();
                foreach (var code in actualSubjectCodes)
                {
                    enrolledSubjects.Add(new SubjectRow
                    {
                        Code = code,
                        Title = code,
                        Units = 0,
                        PreRequisite = ""
                    });
                }

                Console.WriteLine($"[CalculateSecondYearEligibility] Validating {enrolledSubjects.Count} enrolled subjects");

                var (isValid, ongoingSubjects) = EnrollmentRules.ValidateRemarksForNextYearEnrollment(
                    subjectRemarks,
                    enrolledSubjects); // ✅ Use actual enrolled subjects, not canonical list

                if (!isValid)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cannot calculate eligibility. The following subjects are still 'ongoing': {string.Join(", ", ongoingSubjects)}. Please mark them as 'pass' or 'fail'."
                    });
                }

                // Save remarks for enrolled subjects only
                foreach (var kv in subjectRemarks)
                {
                    var code = (kv.Key ?? "").Trim();
                    var val = (kv.Value ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(code)) continue;

                    // ✅ Only save if this is a subject the student was enrolled in
                    if (!actualSubjectCodes.Contains(code))
                    {
                        Console.WriteLine($"[CalculateSecondYearEligibility] Skipping {code} - not in student's schedule");
                        continue;
                    }

                    if (val != "pass" && val != "fail" && val != "ongoing")
                        val = string.IsNullOrEmpty(val) ? "ongoing" : val;

                    request.ExtraFields[$"SubjectRemarks.2ndSem.{code}"] = val; // ✅ Save with 2ndSem prefix
                }

                await _db.SaveStudentSubjectRemarksAsync(
                    student.Username,
                    subjectRemarks.Where(kv => actualSubjectCodes.Contains(kv.Key)) // ✅ Filter
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                    program,
                    currentSemester,
                    currentYearLevel);
            }
            else
            {
                // Mode 2: Auto-fetch from MongoDB
                var mongoRemarks = await _db.GetStudentSubjectRemarksAsync(student.Username);
                if (mongoRemarks != null && mongoRemarks.Any())
                {
                    subjectRemarks = new Dictionary<string, string>();
                    foreach (var remark in mongoRemarks)
                    {
                        if (!string.IsNullOrWhiteSpace(remark.SubjectCode))
                        {
                            var remarkValue = (remark.Remark ?? "ongoing").ToLowerInvariant();
                            request.ExtraFields[$"SubjectRemarks.2ndSem.{remark.SubjectCode}"] = remarkValue;
                            subjectRemarks[remark.SubjectCode] = remarkValue;
                        }
                    }
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "No subject remarks found for 1st Year 2nd Semester. Please set remarks first."
                    });
                }
            }

            // ✅ Get ALL 1st Year remarks (1st sem + 2nd sem) for prerequisite checking
            // ✅ Get ALL 1st Year remarks (1st sem + 2nd sem) from MongoDB + ExtraFields + Archives
            var allFirstYearRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(request, _db);
            // Calculate eligibility using BOTH semesters
            var eligibility = CalculateEligibilityCore(
                program,
                currentYearLevel,
                currentSemester,
                allFirstYearRemarks);

            request.SecondSemesterEligibility = eligibility;
            request.LastUpdatedAt = DateTime.UtcNow;

            var regularity = EnrollmentRules.DetermineRegularity(allFirstYearRemarks);
            request.ExtraFields["CalculatedRegularity"] = regularity;

            await _db.UpdateEnrollmentRequestAsync(request);

            Console.WriteLine($"[CalculateSecondYearEligibility] Calculated for {student.Username}: {eligibility.Count} subjects, Regularity: {regularity}");

            return Ok(new
            {
                success = true,
                eligibility,
                regularity,
                message = $"2nd Year eligibility calculated. Student will be marked as {regularity}."
            });
        }

        // ✅ NEW
        [HttpGet]
        public async Task<IActionResult> TestSecondYearEligibilityDetection(string studentEmail)
        {
            var request = await _db.GetLatestRequestByEmailAsync(studentEmail);
            if (request == null)
                return NotFound("Student not found");

            var settings = await _db.GetEnrollmentSettingsAsync();
            var isEligibleForNextYear = EnrollmentRules.IsEligibleForNextYearEnrollment(request, settings); // ✅ FIXED

            var hasEligibility = request.SecondSemesterEligibility != null && request.SecondSemesterEligibility.Any();

            var subjectRemarks = new Dictionary<string, string>();
            if (request.ExtraFields != null)
            {
                foreach (var kvp in request.ExtraFields)
                {
                    if (kvp.Key.StartsWith("SubjectRemarks."))
                    {
                        subjectRemarks[kvp.Key.Substring("SubjectRemarks.".Length)] = kvp.Value;
                    }
                }
            }

            var hasOngoing = EnrollmentRules.HasOngoingSubjectsBlockingEnrollment(subjectRemarks);
            var regularity = EnrollmentRules.DetermineRegularity(subjectRemarks);

            return Ok(new
            {
                isEligibleForNextYear, // ✅ FIXED variable name
                hasEligibility,
                hasOngoing,
                regularity,
                currentStatus = request.Status,
                currentYear = request.ExtraFields?.GetValueOrDefault("Academic.YearLevel"),
                currentSemester = request.ExtraFields?.GetValueOrDefault("Academic.Semester"),
                eligibilityCount = request.SecondSemesterEligibility?.Count ?? 0,
                remarksCount = subjectRemarks.Count
            });
        }

        private Dictionary<string, string> CalculateEligibilityCore(
      string program,
      string currentYearLevel,
      string currentSemester,
      Dictionary<string, string> subjectRemarks)
        {
            var eligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Determine target year and semester
                var year = EnrollmentRules.ParseYearLevel(currentYearLevel);
                string targetYearLevel;
                string targetSemester;

                if (currentSemester.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                {
                    targetYearLevel = EnrollmentRules.GetNextYearLevel(currentYearLevel);
                    targetSemester = "1st Semester";
                }
                else
                {
                    targetYearLevel = currentYearLevel;
                    targetSemester = "2nd Semester";
                }

                Console.WriteLine($"[CalculateEligibilityCore] Program: {program}, Current: {currentYearLevel} {currentSemester}, Target: {targetYearLevel} {targetSemester}");
                Console.WriteLine($"[CalculateEligibilityCore] Subject remarks count: {subjectRemarks.Count}");

                // Get target subjects
                var targetSubjects = GetSubjectsForYearAndSemester(program, targetYearLevel, targetSemester);
                Console.WriteLine($"[CalculateEligibilityCore] Target subjects count: {targetSubjects.Count}");

                // Get prerequisites map
                var prerequisites = GetPrerequisiteInfo(program, currentYearLevel, currentSemester);
                Console.WriteLine($"[CalculateEligibilityCore] Prerequisites map count: {prerequisites.Count}");

                // Calculate eligibility for each target subject
                foreach (var subject in targetSubjects)
                {
                    var code = subject.Code ?? "";
                    var reason = "Can enroll - No prerequisites";

                    if (prerequisites.TryGetValue(code, out var prereqList) && prereqList != null && prereqList.Count > 0)
                    {
                        var failedPrereqs = new List<string>();

                        foreach (var prereqCode in prereqList)
                        {
                            if (!subjectRemarks.TryGetValue(prereqCode, out var remark) ||
                                !string.Equals(remark ?? "", "pass", StringComparison.OrdinalIgnoreCase))
                            {
                                failedPrereqs.Add(prereqCode);
                            }
                        }

                        if (failedPrereqs.Any())
                        {
                            reason = $"Cannot enroll - Prerequisites not met: {string.Join(", ", failedPrereqs)}";
                        }
                        else
                        {
                            reason = "Can enroll - All prerequisites passed";
                        }
                    }

                    eligibility[code] = reason;
                    Console.WriteLine($"[CalculateEligibilityCore] {code}: {reason}");
                }

                return eligibility;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CalculateEligibilityCore] ERROR: {ex.Message}");
                Console.WriteLine($"[CalculateEligibilityCore] Stack trace: {ex.StackTrace}");
                throw; // Re-throw to trigger 500 error with proper logging
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalculateTransfereeEligibility(string id, [FromForm] Dictionary<string, string>? subjectRemarks)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest(new { success = false, message = "Invalid request id." });

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
                return NotFound(new { success = false, message = "Enrollment request not found." });

            // Guard: Only for Transferee
            if (string.IsNullOrWhiteSpace(request.Type) ||
                !request.Type.Equals("Transferee", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { success = false, message = "Use CalculateEligibility for non-transferee records." });
            }

            // Initialize ExtraFields if needed
            request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Transferees MUST provide manual remarks (from TOR evaluation)
            if (subjectRemarks == null || !subjectRemarks.Any())
            {
                return BadRequest(new { success = false, message = "Subject remarks are required for transferee eligibility calculation. Please evaluate the TOR first." });
            }

            // Save remarks to ExtraFields
            foreach (var kv in subjectRemarks)
            {
                var code = (kv.Key ?? "").Trim();
                var val = (kv.Value ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(code)) continue;

                // Normalize to pass/fail/ongoing
                if (val != "pass" && val != "fail" && val != "ongoing")
                    val = string.IsNullOrEmpty(val) ? "ongoing" : val;

                request.ExtraFields[$"SubjectRemarks.{code}"] = val;
            }

            // Get program once
            var program = GetProgramForRequest(request);
            Console.WriteLine($"[CalculateTransfereeEligibility] Program: {program}");
            Console.WriteLine($"[CalculateTransfereeEligibility] Subject remarks count: {subjectRemarks.Count}");

            // Save to MongoDB collection for consistency
            var student = await _db.GetStudentByEmailAsync(request.Email);
            if (student != null)
            {
                var semester = request.ExtraFields.TryGetValue("Academic.Semester", out var sem) ? sem : "1st Semester";
                var yearLevel = request.ExtraFields.TryGetValue("Academic.YearLevel", out var yl) ? yl : "1st Year";

                await _db.SaveStudentSubjectRemarksAsync(
                    student.Username,
                    subjectRemarks,
                    program,
                    semester,
                    yearLevel);
            }

            // ✅ CRITICAL FIX: Get 2nd semester subjects with their PreRequisite property
            var secondSemSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects
                : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects;

            Console.WriteLine($"[CalculateTransfereeEligibility] Evaluating {secondSemSubjects.Count} 2nd semester subjects");

            // Calculate eligibility using subject's PreRequisite property directly
            var eligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subject in secondSemSubjects)
            {
                var code = subject.Code ?? "";
                var prereqString = subject.PreRequisite ?? "";

                Console.WriteLine($"[CalculateTransfereeEligibility] Subject {code}: PreRequisite = '{prereqString}'");

                if (string.IsNullOrWhiteSpace(prereqString))
                {
                    eligibility[code] = "Can enroll - No prerequisites";
                    Console.WriteLine($"  → {code}: No prerequisites");
                    continue;
                }

                // Parse prerequisites from the subject model
                var prereqList = prereqString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                if (!prereqList.Any())
                {
                    eligibility[code] = "Can enroll - No prerequisites";
                    Console.WriteLine($"  → {code}: Empty prereq list after parsing");
                    continue;
                }

                Console.WriteLine($"  → {code}: Prerequisites: {string.Join(", ", prereqList)}");

                // Check if all prerequisites are passed
                var failedPrereqs = new List<string>();
                foreach (var prereqCode in prereqList)
                {
                    // Look up remark in ExtraFields
                    var remarkKey = $"SubjectRemarks.{prereqCode}";
                    if (!request.ExtraFields.TryGetValue(remarkKey, out var remark) ||
                        !string.Equals(remark ?? "", "pass", StringComparison.OrdinalIgnoreCase))
                    {
                        failedPrereqs.Add(prereqCode);
                        Console.WriteLine($"    ✗ {prereqCode}: NOT passed (remark = '{remark}')");
                    }
                    else
                    {
                        Console.WriteLine($"    ✓ {prereqCode}: PASSED");
                    }
                }

                if (failedPrereqs.Any())
                {
                    eligibility[code] = $"Cannot enroll - Prerequisites not met: {string.Join(", ", failedPrereqs)}";
                }
                else
                {
                    eligibility[code] = "Can enroll - All prerequisites passed";
                }

                Console.WriteLine($"  → {code}: {eligibility[code]}");
            }

            // Save eligibility and updated remarks
            request.SecondSemesterEligibility = eligibility;
            request.LastUpdatedAt = DateTime.UtcNow;
            await _db.UpdateEnrollmentRequestAsync(request);

            Console.WriteLine($"[CalculateTransfereeEligibility] ✅ Saved {eligibility.Count} eligibility entries");

            return Ok(new { success = true, eligibility });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllowResubmission(string id, int tokenDays = 7, [FromForm] Dictionary<string, string>? flags = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Enrollment request not found.";
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            if (string.IsNullOrWhiteSpace(request.Status) || !request.Status.StartsWith("Rejected", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Allow resubmission is only available for rejected requests.";
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            MergePostedFlags(request, flags);
            if (!AreFlagsComplete(request))
            {
                TempData["Error"] = "Flag all required documents before allowing resubmission.";
                return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
            }

            if (tokenDays < 1) tokenDays = 7;

            request.EditToken = Guid.NewGuid().ToString("N");
            request.EditTokenExpires = DateTime.UtcNow.AddDays(tokenDays);
            request.LastUpdatedAt = DateTime.UtcNow;

            await _db.UpdateEnrollmentRequestAsync(request);

            var action = string.Equals(request.Type, "Transferee", StringComparison.OrdinalIgnoreCase)
                ? "EditTransfereeEnrollment"
                : "EditEnrollment";
            var editLink = Url.Action(action, "EditEnrollments", new { area = "Student", token = request.EditToken }, Request.Scheme);

            var flagsSummary = BuildFlagsSummary(request);

            try
            {
                await _email.SendResubmissionEmailDetailedAsync(
                    request.Email,
                    request.Reason ?? "Rejected",
                    flagsSummary,
                    request.Notes,
                    editLink!,
                    tokenDays
                );
                TempData["Info"] = $"Resubmission allowed. Link sent; valid for {tokenDays} day(s).";
            }
            catch
            {
                TempData["Error"] = "Resubmission allowed, but sending the email failed.";
            }

            return RedirectToAction(nameof(RequestDetails), new { id = request.Id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDocumentFlags(
     string id,
     [FromForm] Dictionary<string, string> flags,
     [FromForm] Dictionary<string, string> subjectRemarks,
     [FromForm] Dictionary<string, string> secondSemEligibility)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Enrollment request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Merge posted flags
            MergePostedFlags(request, flags);

            // Save subject remarks to ExtraFields AND to student_subject_remarks collection
            if (subjectRemarks != null && subjectRemarks.Any())
            {
                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var remark in subjectRemarks)
                {
                    var subjectCode = (remark.Key ?? "").Trim();
                    var remarkValue = remark.Value ?? "";
                    if (string.IsNullOrEmpty(subjectCode)) continue;
                    request.ExtraFields[$"SubjectRemarks.{subjectCode}"] = remarkValue;
                }

                // Save to student_subject_remarks collection
                var student = await _db.GetStudentByEmailAsync(request.Email);
                if (student != null)
                {
                    var program = GetProgramForRequest(request);
                    var semester = request.ExtraFields.TryGetValue("Academic.Semester", out var sem) ? sem : "1st Semester";
                    var yearLevel = request.ExtraFields.TryGetValue("Academic.YearLevel", out var yl) ? yl : "1st Year";

                    await _db.SaveStudentSubjectRemarksAsync(
                        student.Username,
                        subjectRemarks,
                        program,
                        semester,
                        yearLevel);
                }
            }

            // Sanitize and save 2nd semester eligibility
            bool eligibilitySaved = false;
            if (secondSemEligibility != null && secondSemEligibility.Any())
            {
                var program = GetProgramForRequest(request);
                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var secondSemSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects;

                if (secondSemSubjects != null)
                {
                    foreach (var s in secondSemSubjects)
                    {
                        if (!string.IsNullOrWhiteSpace(s.Code))
                            allowed.Add(s.Code.Trim());
                    }
                }

                var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in secondSemEligibility)
                {
                    var key = (kv.Key ?? "").Trim();
                    if (string.IsNullOrEmpty(key)) continue;
                    if (!allowed.Contains(key)) continue;
                    sanitized[key] = kv.Value ?? "";
                }

                if (sanitized.Any())
                {
                    request.SecondSemesterEligibility = sanitized;
                    eligibilitySaved = true;
                }
                else
                {
                    request.SecondSemesterEligibility = null;
                }
            }

            request.LastUpdatedAt = DateTime.UtcNow;
            await _db.UpdateEnrollmentRequestAsync(request);

            // ✅ NEW: Provide detailed success message based on what was saved
            if (eligibilitySaved && subjectRemarks != null && subjectRemarks.Any())
            {
                TempData["Success"] = "✅ Subject remarks and eligibility successfully saved! The eligibility calculation has been stored and will be used for enrollment.";
            }
            else if (eligibilitySaved)
            {
                TempData["Success"] = "✅ Eligibility successfully saved! The eligibility calculation has been stored.";
            }
            else if (subjectRemarks != null && subjectRemarks.Any())
            {
                TempData["Success"] = "✅ Subject remarks successfully saved!";
            }
            else
            {
                TempData["Info"] = "Document flags updated.";
            }

            return RedirectToAction(nameof(EnrolledDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRequest(string id, string status, string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Enrollment request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            if (!string.IsNullOrWhiteSpace(status))
                request.Status = status;

            request.Reason = reason;
            request.LastUpdatedAt = DateTime.UtcNow;

            await _db.UpdateEnrollmentRequestAsync(request);
            TempData["Info"] = "Request updated.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(AdminScheme);
            return RedirectToAction(nameof(AdminLogin));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetDatabase(bool seed = false)
        {
            try
            {
                await _db.ResetDatabaseAsync(reseed: seed, year: DateTime.UtcNow.Year, program: "BSIT", sectionCapacity: 1);
                TempData["Info"] = $"All MongoDB collections dropped{(seed ? " and base data reseeded." : ".")}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to drop collections: {ex.Message}";
            }
            return RedirectToAction(nameof(AdminSettings));
        }

        private IActionResult RedirectToStatusList(string? status)
        {
            var s = (status ?? "").Trim();

            // Map by known statuses
            if (s.Equals("1st Sem Pending", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Pending1stSem", "Admin", new { area = "Admin" });

            if (s.Equals("2nd Sem Pending", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Pending2ndSem", "Admin", new { area = "Admin" });

            if (s.Equals("On Hold", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("OnHold", "Admin", new { area = "Admin" });

            if (s.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Rejected", "Admin", new { area = "Admin" });

            // Route both regular and irregular 2nd-sem enrolled to the same 2nd sem list
            if (s.StartsWith("Enrolled - Regular", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Enrolled - Irregular", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Enrolled2ndSem", "Admin", new { area = "Admin" });

            if (s.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Enrolled", "Admin", new { area = "Admin" });

            // Fallback
            return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });
        }

        private IActionResult RedirectToListFor(EnrollmentRequest? req, string? fallbackStatus = null)
            => RedirectToStatusList(req?.Status ?? fallbackStatus ?? "1st Sem Pending");

        /// <summary>
        /// GET: Request details for 2nd Year enrollment requests
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RequestDetails2ndYear(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            var program = GetProgramForRequest(request);
            ViewBag.ProgramNormalized = program;

            // Get 2nd Year 1st Semester subjects
            var secondYearSubjects = GetSubjectsForYearAndSemester(program, "2nd Year", "1st Semester");
            ViewBag.SecondYearSubjects = secondYearSubjects;

            // Get calculated eligibility from request
            var eligibility = request.SecondSemesterEligibility ?? new Dictionary<string, string>();
            ViewBag.SecondYearEligibility = eligibility;

            // Get all 1st Year remarks for display
            // Get all 1st Year remarks for display
            var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(request, _db);
            ViewBag.AllFirstYearRemarks = allRemarks;

            // Get prerequisite map
            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);
            ViewBag.PrerequisiteMap = prereqMap;

            // Count eligible vs ineligible subjects
            var eligibleCount = eligibility.Count(kvp => kvp.Value.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase));
            var ineligibleCount = eligibility.Count - eligibleCount;
            ViewBag.EligibleCount = eligibleCount;
            ViewBag.IneligibleCount = ineligibleCount;

            var settings = await _db.GetEnrollmentSettingsAsync();
            ViewBag.EnrollmentSettings = settings;

            ViewData["Title"] = "2nd Year Enrollment Request";
            return View("~/Areas/Admin/Views/Admin/RequestDetails2ndYear.cshtml", request);
        }

        [HttpGet]
        public async Task<IActionResult> RequestDetails(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid request id.";
                return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });
            }

            var req = await _db.GetEnrollmentRequestByIdAsync(id);
            if (req is null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("Dashboard", "Admin", new { area = "Admin" });
            }

            // ✅ NEW: Detect 2nd Year enrollment requests
            var is2ndYearRequest = req.Type != null &&
                (req.Type.Contains("2nd Year-Regular", StringComparison.OrdinalIgnoreCase) ||
                 req.Type.Contains("2nd Year-Irregular", StringComparison.OrdinalIgnoreCase));

            if (is2ndYearRequest)
            {
                // Redirect to specialized 2nd Year request details view
                return RedirectToAction(nameof(RequestDetails2ndYear), new { id });
            }
            // ✅ NEW: Redirect enrolled records to EnrolledDetails action
            if (!string.IsNullOrWhiteSpace(req.Status) &&
                req.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(EnrolledDetails), new { id });
            }
            // ✅ NEW: Redirect enrolled records to EnrolledDetails action
            if (!string.IsNullOrWhiteSpace(req.Status) &&
                req.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(EnrolledDetails), new { id });
            }

            // Load subject data for the student (if any)
            req = await MergeSchoolDataFrom1stSemesterAsync(req);

            // Determine program for this request so prerequisites and subject lookups are program-aware
            var programForReq = GetProgramForRequest(req);
            ViewBag.ProgramNormalized = programForReq;
            // Ensure existing subject remarks and prerequisite info are always available to the view.
            // This fixes the issue where remarks reverted to "ongoing" on refresh because ViewBag.ExistingSubjectRemarks
            // was only populated when a student schedule existed.
            ViewBag.ExistingSubjectRemarks = GetExistingSubjectRemarks(req);
            ViewBag.Prerequisites = GetPrerequisiteInfo(programForReq);

            //Load subject data for the student (if any)
            var student = await _db.GetStudentByEmailAsync(req.Email);
            if (student != null)
            {
                var subjectSchedules = await _db.GetStudentScheduleAsync(student.Username);
                if (subjectSchedules != null && subjectSchedules.Any())
                {
                    var roomNames = await _db.GetRoomNamesByIdsAsync(subjectSchedules.Select(m => m.RoomId));

                    // Use program to select canonical subject titles/units
                    var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);
                    if (string.Equals(programForReq, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                        foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }
                    else
                    {
                        foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                        foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                            subjectDict[s.Code] = (s.Title, s.Units);
                    }

                    static string DayName(int d) => d switch
                    {
                        1 => "Mon",
                        2 => "Tue",
                        3 => "Wed",
                        4 => "Thu",
                        5 => "Fri",
                        6 => "Sat",
                        7 => "Sun",
                        _ => "-"
                    };

                    var subjects = new List<dynamic>();
                    foreach (var schedule in subjectSchedules)
                    {
                        var code = schedule.CourseCode ?? "";
                        subjectDict.TryGetValue(code, out var meta);

                        subjects.Add(new
                        {
                            Code = code,
                            Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                            Units = meta.Units,
                            Day = DayName(schedule.DayOfWeek),
                            Time = schedule.DisplayTime ?? "",
                            Room = roomNames.TryGetValue(schedule.RoomId ?? "", out var rn) ? rn : (schedule.RoomId ?? "")
                        });
                    }

                    ViewBag.SubjectSchedules = subjects;
                    // ViewBag.ExistingSubjectRemarks already set above; keep for compatibility
                    // ViewBag.Prerequisites already set above; keep for compatibility
                }
            }

            if (req.SecondSemesterEligibility != null && req.SecondSemesterEligibility.Any())
            {
                ViewBag.ExistingSecondSemEligibility = req.SecondSemesterEligibility;
            }

            var settings = await _db.GetEnrollmentSettingsAsync();
            ViewBag.EnrollmentSettings = settings;

            // Provide capacity info for the request's program
            var program = (req.Program ?? "").Trim();
            if (!string.IsNullOrEmpty(program))
            {
                if (settings.ProgramCapacities != null && settings.ProgramCapacities.TryGetValue(program, out var cap) && cap > 0)
                {
                    var enrolled = await _db.CountEnrolledByProgramAsync(program);
                    ViewBag.ProgramCapacityProgram = program;
                    ViewBag.ProgramCapacityLimit = cap;
                    ViewBag.ProgramCapacityEnrolled = enrolled;
                    ViewBag.ProgramCapacityFull = enrolled >= cap;
                }
            }

            // ✅ NEW: Fetch required documents from MongoDB for dynamic display
            try
            {
                var requiredDocs = await _db.GetRequiredDocumentsAsync(); // Uses MongoDB service method
                ViewBag.RequiredDocuments = requiredDocs ?? new List<RequiredDocument>();

                Console.WriteLine($"✅ Loaded {requiredDocs?.Count ?? 0} documents from documents_required.");
            }
            catch (Exception ex)
            {
                // Safe fallback — prevent crash if collection not found
                Console.WriteLine($"⚠️ Failed to load documents_required: {ex.Message}");
                ViewBag.RequiredDocuments = new List<RequiredDocument>();
            }

            // If the record is already enrolled, render the EnrolledDetails view (separates enrolled record UI)
            // If the record is already enrolled, render the EnrolledDetails view
            if (!string.IsNullOrWhiteSpace(req.Status) &&
                req.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["Title"] = "Enrolled Details";
                return View("~/Areas/Admin/Views/Admin/EnrolledDetails.cshtml", req);
            }

            ViewData["Title"] = "Request Details";

            // Choose view based on request type
            if (string.Equals(req.Type, "Transferee", StringComparison.OrdinalIgnoreCase))
            {
                return View("~/Areas/Admin/Views/Admin/RequestDetailsTransferee.cshtml", req);
            }

            return View(req);
        }


        private async Task<EnrollmentRequest> MergeSchoolDataFrom1stSemesterAsync(EnrollmentRequest request)
        {
            // Only apply to 2nd semester requests
            if (!IsSecondSemRequest(request))
                return request;

            // Check if school data is already present
            var hasSchoolData = request.ExtraFields != null && (
                request.ExtraFields.ContainsKey("ElementarySchool.SchoolName") ||
                request.ExtraFields.ContainsKey("HighSchool.SchoolName") ||
                request.ExtraFields.ContainsKey("SeniorHigh.SchoolName")
            );

            if (hasSchoolData)
                return request; // Already has data, no need to merge

            try
            {
                // Find the student's 1st semester enrollment
                var settings = await _db.GetEnrollmentSettingsAsync();
                var currentAY = settings.AcademicYear ?? "";

                // Get all requests for this email using the existing method
                var allRequests = await _db.GetRequestsByEmailAsync(request.Email);

                if (allRequests == null || !allRequests.Any())
                    return request;

                // Find a 1st semester request with school data from the current academic year
                var firstSemRequest = allRequests.FirstOrDefault(r =>
                    r.Id != request.Id && // Don't merge with itself
                    r.ExtraFields != null &&
                    r.ExtraFields.TryGetValue("Academic.AcademicYear", out var ay) &&
                    ay == currentAY &&
                    r.ExtraFields.TryGetValue("Academic.Semester", out var sem) &&
                    sem.StartsWith("1", StringComparison.OrdinalIgnoreCase) &&
                    (r.ExtraFields.ContainsKey("ElementarySchool.SchoolName") ||
                     r.ExtraFields.ContainsKey("HighSchool.SchoolName") ||
                     r.ExtraFields.ContainsKey("SeniorHigh.SchoolName"))
                );

                // If no current AY match, try to find ANY 1st semester request with school data
                if (firstSemRequest == null)
                {
                    firstSemRequest = allRequests.FirstOrDefault(r =>
                        r.Id != request.Id &&
                        r.ExtraFields != null &&
                        (r.Status?.Contains("Enrolled", StringComparison.OrdinalIgnoreCase) == true ||
                         r.Status?.Contains("1st Sem", StringComparison.OrdinalIgnoreCase) == true) &&
                        (r.ExtraFields.ContainsKey("ElementarySchool.SchoolName") ||
                         r.ExtraFields.ContainsKey("HighSchool.SchoolName") ||
                         r.ExtraFields.ContainsKey("SeniorHigh.SchoolName"))
                    );
                }

                if (firstSemRequest?.ExtraFields == null)
                    return request; // No 1st semester data found

                // Merge school-related fields
                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var schoolPrefixes = new[] {
            "ElementarySchool", "ElementaryAddress",
            "HighSchool", "HighSchoolAddress",
            "SeniorHigh", "SeniorHighAddress"
        };

                int mergedCount = 0;
                foreach (var kvp in firstSemRequest.ExtraFields)
                {
                    var shouldCopy = schoolPrefixes.Any(prefix =>
                        kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                    if (shouldCopy && !request.ExtraFields.ContainsKey(kvp.Key))
                    {
                        request.ExtraFields[kvp.Key] = kvp.Value;
                        mergedCount++;
                    }
                }

                // Log the merge for debugging
                if (mergedCount > 0)
                {
                    Console.WriteLine($"[MergeSchoolData] Merged {mergedCount} school fields from request {firstSemRequest.Id} to {request.Id}");
                }
                else
                {
                    Console.WriteLine($"[MergeSchoolData] Found 1st semester request {firstSemRequest.Id} but no school fields needed merging for {request.Id}");
                }
            }
            catch (Exception ex)
            {
                // Don't fail the entire request if merge fails
                Console.WriteLine($"[MergeSchoolData] Error merging school data: {ex.Message}");
            }

            return request;
        }

        private Dictionary<string, string> GetExistingSubjectRemarks(EnrollmentRequest request)
        {
            var remarks = new Dictionary<string, string>();
            if (request.ExtraFields != null)
            {
                foreach (var kvp in request.ExtraFields)
                {
                    if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    {
                        var subjectCode = kvp.Key.Substring("SubjectRemarks.".Length);
                        remarks[subjectCode] = kvp.Value;
                    }
                }
            }
            return remarks;
        }


        // Program-aware prerequisite map
        // Replace the existing GetPrerequisiteInfo method (around line 1200)

        /// <summary>
        /// Get prerequisite map for subjects (year-level aware)
        /// </summary>
        private Dictionary<string, List<string>> GetPrerequisiteInfo(string? program = null, string? yearLevel = null, string? semester = null)
        {
            var p = NormalizeProgramCode(program ?? "BSIT");
            var year = EnrollmentRules.ParseYearLevel(yearLevel ?? "1st Year");
            var sem = semester ?? "2nd Semester"; // Default to 2nd semester for eligibility calculation

            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Get the target subjects (next semester subjects for eligibility)
                List<SubjectRow> targetSubjects;

                // If checking eligibility for next year, get the 1st semester of next year
                if (sem.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                {
                    targetSubjects = GetSubjectsForYearAndSemester(p, EnrollmentRules.GetNextYearLevel(yearLevel ?? "1st Year"), "1st Semester");
                }
                else
                {
                    targetSubjects = GetSubjectsForYearAndSemester(p, yearLevel ?? "1st Year", "2nd Semester");
                }

                // Build prerequisite map from subject PreRequisite field
                foreach (var subject in targetSubjects)
                {
                    if (string.IsNullOrWhiteSpace(subject.PreRequisite)) continue;

                    var prereqs = subject.PreRequisite
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    if (prereqs.Count > 0 && !string.IsNullOrWhiteSpace(subject.Code))
                    {
                        map[subject.Code] = prereqs;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetPrerequisiteInfo] Error: {ex.Message}");
            }

            return map;
        }

        // Normalizes and derives a canonical program code for a request.
        // Maps common variants (e.g., "BSENTREP") to canonical "BSENT" and falls back to "BSIT".
        private string GetProgramForRequest(EnrollmentRequest request)
        {
            if (request == null) return "BSIT";
            var program = (request.Program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(program) && request.ExtraFields != null &&
                request.ExtraFields.TryGetValue("Academic.Program", out var p) && !string.IsNullOrWhiteSpace(p))
            {
                program = p.Trim();
            }
            return NormalizeProgramCode(program);
        }

        // Basic normalization rules - extend as needed for other legacy values.
        private string NormalizeProgramCode(string? program)
        {
            var p = (program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return "BSIT";

            p = p.ToUpperInvariant();

            // Common legacy or variant forms => canonical
            if (p.Contains("BSENT", StringComparison.OrdinalIgnoreCase)) return "BSENT";
            if (p.Contains("BSIT", StringComparison.OrdinalIgnoreCase)) return "BSIT";

            // If unknown but matches catalog, return uppercase variant; otherwise default to BSIT
            if (ProgramCatalog.All.Any(x => string.Equals(x.Code, p, StringComparison.OrdinalIgnoreCase)))
                return ProgramCatalog.All.First(x => string.Equals(x.Code, p, StringComparison.OrdinalIgnoreCase)).Code;

            return "BSIT";
        }
        [HttpGet]
        public async Task<IActionResult> OnHoldShifter(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetShifterEnrollmentRequestsByStatusAsync("On-Hold")
                : await SearchShifterRequestsByStatusAsync("On-Hold", q, program);

            ViewBag.Title = "On-Hold Shifter Requests";
            ViewBag.FilterAction = "OnHoldShifter";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            ViewBag.EnrollmentSettings = await _db.GetEnrollmentSettingsAsync();

            return View("OnHoldShifter", items);
        }



        [HttpGet]
        public async Task<IActionResult> RequestDetailsShifter(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid shifter request id.";
                return RedirectToAction(nameof(Dashboard));
            }

            var request = await _db.GetShifterEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Shifter enrollment request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Get student's current program info
            EnrollmentRequest? enrollment = null;
            var student = await _db.GetStudentByUsernameAsync(request.StudentUsername);
            if (student != null)
            {
                enrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
                ViewBag.CurrentEnrollment = enrollment;
            }

            // Get subject remarks for the student
            var subjectRemarks = await _db.GetStudentSubjectRemarksAsync(request.StudentUsername);
            ViewBag.SubjectRemarks = subjectRemarks ?? new List<StudentSubjectRemarks>();

            // Get subjects from both programs for comparison
            var currentProgramSubjects = GetProgramSubjects(request.CourseLastEnrolled, "1st Semester");
            var targetProgramSubjects = GetProgramSubjects(request.CourseApplyingToShift, "1st Semester");

            ViewBag.CurrentProgramSubjects = currentProgramSubjects ?? new List<SubjectRow>();
            ViewBag.TargetProgramSubjects = targetProgramSubjects ?? new List<SubjectRow>();

            // ✅ STORE matchedSubjects in a variable so we can debug it
            var matchedSubjects = GetMatchedSubjects(
                currentProgramSubjects ?? new List<SubjectRow>(),
                targetProgramSubjects ?? new List<SubjectRow>(),
                subjectRemarks ?? new List<StudentSubjectRemarks>());

            ViewBag.MatchedSubjects = matchedSubjects;

            // ✅ DEBUG OUTPUT (now matchedSubjects is in scope)
            Console.WriteLine($"=== Debug: Shifter Request {id} ===");
            Console.WriteLine($"Current Program: {request.CourseLastEnrolled}");
            Console.WriteLine($"Target Program: {request.CourseApplyingToShift}");
            Console.WriteLine($"Subject Remarks Count: {(subjectRemarks?.Count ?? 0)}");

            if (subjectRemarks != null)
            {
                foreach (var r in subjectRemarks)
                {
                    Console.WriteLine($"  - {r.SubjectCode}: {r.Remark} (Program: {r.Program ?? "N/A"})");
                }
            }

            Console.WriteLine($"Current Program Subjects Count: {(currentProgramSubjects?.Count ?? 0)}");
            if (currentProgramSubjects != null)
            {
                foreach (var s in currentProgramSubjects)
                {
                    Console.WriteLine($"  - {s.Code}: {s.Title}");
                }
            }

            Console.WriteLine($"Target Program Subjects Count: {(targetProgramSubjects?.Count ?? 0)}");
            if (targetProgramSubjects != null)
            {
                foreach (var s in targetProgramSubjects)
                {
                    Console.WriteLine($"  - {s.Code}: {s.Title}");
                }
            }

            Console.WriteLine($"Matched Subjects Count: {matchedSubjects.Count}");
            foreach (var m in matchedSubjects)
            {
                Console.WriteLine($"  - {m.SubjectCode}: IsMatched={m.IsMatched}, IsPassed={m.IsPassed}, IsCredited={m.IsCredited}, Remark={m.Remark}");
            }

            ViewData["Title"] = "Shifter Request Details";
            return View("RequestDetailsShifter", request);
        }

        /// <summary>
        /// Accept a shifter enrollment request
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptShifterRequest(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid shifter request id.";
                return RedirectToAction(nameof(PendingShifter));
            }

            var request = await _db.GetShifterEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Shifter enrollment request not found.";
                return RedirectToAction(nameof(PendingShifter));
            }

            // Validate documents are submitted
            if (string.IsNullOrWhiteSpace(request.EndorsementLetterPath) ||
                string.IsNullOrWhiteSpace(request.LibraryClearancePath))
            {
                TempData["Error"] = "Both Endorsement Letter and Library Clearance must be submitted before accepting.";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }

            var student = await _db.GetStudentByUsernameAsync(request.StudentUsername);
            if (student == null)
            {
                TempData["Error"] = "Student account not found.";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }

            try
            {
                // ✅ FIX: Declare subjectRemarks OUTSIDE the if block so it's available everywhere
                var subjectRemarks = await _db.GetStudentSubjectRemarksAsync(request.StudentUsername);

                // Update student record
                student.Type = "Shifter";

                // Get their current enrollment record
                var currentEnrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
                if (currentEnrollment != null)
                {
                    // Update enrollment record to reflect shift
                    currentEnrollment.Program = request.CourseApplyingToShift;
                    currentEnrollment.Type = "Shifter";

                    // Mark as irregular if they have credited subjects
                    var hasCreditedSubjects = HasCreditedSubjects(
                        request.CourseLastEnrolled,
                        request.CourseApplyingToShift,
                        subjectRemarks);

                    // Update extra fields
                    currentEnrollment.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    currentEnrollment.ExtraFields["Academic.Program"] = request.CourseApplyingToShift;
                    currentEnrollment.ExtraFields["Academic.YearLevel"] = "1st Year";
                    currentEnrollment.ExtraFields["Academic.Semester"] = "1st Semester";
                    currentEnrollment.ExtraFields["EnrollmentType"] = hasCreditedSubjects ? "Irregular" : "Regular";
                    currentEnrollment.ExtraFields["PreviousProgram"] = request.CourseLastEnrolled;
                    currentEnrollment.ExtraFields["ShiftedDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd");

                    await _db.UpdateEnrollmentRequestAsync(currentEnrollment);
                }

                // Create new section assignment for shifter
                var settings = await _db.GetEnrollmentSettingsAsync();
                var newProgram = request.CourseApplyingToShift;

                // Get eligible subjects (all 1st sem subjects minus credited ones)
                var targetSubjects = GetProgramSubjects(newProgram, "1st Semester");

                // ✅ FIX: Now subjectRemarks is in scope here
                var creditedSubjectCodes = GetCreditedSubjectCodes(
                    request.CourseLastEnrolled,
                    request.CourseApplyingToShift,
                    subjectRemarks);

                var eligibleSubjects = targetSubjects
                    .Where(s => !creditedSubjectCodes.Contains(s.Code, StringComparer.OrdinalIgnoreCase))
                    .Select(s => s.Code)
                    .ToList();

                // Clear existing student schedule and reassign
                await _db.ClearStudentScheduleAsync(student.Username);

                // Create custom section for shifter
                var sectionName = $"Shifter-{student.Username}-{newProgram}";
                var customSection = new CourseSection
                {
                    Id = $"{newProgram}-{DateTime.UtcNow.Year}-Shifter-{student.Username}",
                    Program = newProgram,
                    Name = sectionName,
                    Capacity = 1,
                    CurrentCount = 0,
                    Year = DateTime.UtcNow.Year
                };

                await _db.CreateCustomSectionAsync(customSection);
                await _db.GenerateSectionScheduleAsync(customSection.Id, eligibleSubjects);
                await _db.EnrollStudentInSectionAsync(student.Username, customSection.Id);

                // Update student record
                await _db.UpdateStudentAsync(student);

                // Update shifter request status
                request.Status = "Accepted";
                request.ReviewedDate = DateTime.UtcNow;
                request.AdminNotes = $"Accepted on {DateTime.UtcNow:yyyy-MM-dd}. Student shifted from {request.CourseLastEnrolled} to {request.CourseApplyingToShift}.";
                await _db.UpdateShifterEnrollmentRequestAsync(request);

                // Generate registration slip PDF
                byte[]? pdfBytes = null;
                try
                {
                    if (currentEnrollment != null)
                    {
                        pdfBytes = await _pdf.GenerateForRequestAsync(currentEnrollment.Id);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to generate PDF: {ex.Message}");
                }

                // Send acceptance email
                try
                {
                    await _email.SendAcceptanceEmailAsync(
                        request.Email,
                        student.Username,
                        null!, // No new password for existing student
                        pdfBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send email: {ex.Message}");
                    TempData["Warning"] = "Shifter accepted but email notification failed.";
                }

                TempData["Success"] = $"Shifter request accepted. {student.Username} has been shifted to {request.CourseApplyingToShift}.";
                return RedirectToAction(nameof(AcceptedShifter));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to accept shifter request: {ex.Message}";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }
        }

        /// <summary>
        /// Reject a shifter enrollment request
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectShifterRequest(string id, string reason, string? notes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid shifter request id.";
                return RedirectToAction(nameof(PendingShifter));
            }

            var request = await _db.GetShifterEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Shifter enrollment request not found.";
                return RedirectToAction(nameof(PendingShifter));
            }

            reason = (reason ?? "").Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Rejection reason is required.";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }

            try
            {
                request.Status = "Rejected";
                request.AdminNotes = reason;
                if (!string.IsNullOrWhiteSpace(notes))
                    request.AdminNotes += $"\nNotes: {notes.Trim()}";

                request.ReviewedDate = DateTime.UtcNow;
                await _db.UpdateShifterEnrollmentRequestAsync(request);

                // Send rejection email
                try
                {
                    await _email.SendRejectionEmailAsync(request.Email, reason, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send rejection email: {ex.Message}");
                    TempData["Warning"] = "Shifter rejected but email notification failed.";
                }

                TempData["Success"] = "Shifter request rejected and notification sent.";
                return RedirectToAction(nameof(RejectedShifter));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to reject shifter request: {ex.Message}";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }
        }

      
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoldShifterRequest(string id, string reason)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                TempData["Error"] = "Invalid shifter request id.";
                return RedirectToAction(nameof(PendingShifter));
            }

            var request = await _db.GetShifterEnrollmentRequestByIdAsync(id);
            if (request == null)
            {
                TempData["Error"] = "Shifter enrollment request not found.";
                return RedirectToAction(nameof(PendingShifter));
            }

            reason = (reason ?? "Awaiting further review").Trim();

            try
            {
                request.Status = "On-Hold";
                request.AdminNotes = reason;
                request.ReviewedDate = DateTime.UtcNow;
                await _db.UpdateShifterEnrollmentRequestAsync(request);

                TempData["Info"] = "Shifter request placed on hold.";
                return RedirectToAction(nameof(OnHoldShifter));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to hold shifter request: {ex.Message}";
                return RedirectToAction(nameof(RequestDetailsShifter), new { id });
            }
        }

        // =========================================================
        // ✅ HELPER METHODS FOR SHIFTER ENROLLMENT
        // =========================================================

        /// <summary>
        /// Search shifter requests by status with optional filters
        /// </summary>
        private async Task<List<ShifterEnrollmentRequest>> SearchShifterRequestsByStatusAsync(
            string status,
            string? searchTerm,
            string? program)
        {
            var allRequests = await _db.GetShifterEnrollmentRequestsByStatusAsync(status);

            if (string.IsNullOrWhiteSpace(searchTerm) && string.IsNullOrWhiteSpace(program))
                return allRequests;

            var filtered = allRequests.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(r =>
                    r.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    r.StudentUsername.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(program))
            {
                var prog = program.Trim();
                filtered = filtered.Where(r =>
                    r.CourseLastEnrolled.Equals(prog, StringComparison.OrdinalIgnoreCase) ||
                    r.CourseApplyingToShift.Equals(prog, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        /// <summary>
        /// Get subjects for a specific program and semester
        /// </summary>
        private List<SubjectRow> GetProgramSubjects(string program, string semester)
        {
            var prog = (program ?? "BSIT").Trim();
            var sem = (semester ?? "1st Semester").Trim();

            if (prog.Equals("BSENT", StringComparison.OrdinalIgnoreCase))
            {
                if (sem.StartsWith("1st", StringComparison.OrdinalIgnoreCase))
                    return E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects.ToList();
                else
                    return E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
            }
            else // Default to BSIT
            {
                if (sem.StartsWith("1st", StringComparison.OrdinalIgnoreCase))
                    return E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects.ToList();
                else
                    return E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
            }
        }

        private List<ShifterSubjectMatchViewModel> GetMatchedSubjects(
    List<SubjectRow> currentProgram,
    List<SubjectRow> targetProgram,
    List<StudentSubjectRemarks> remarks)
        {
            var matches = new List<ShifterSubjectMatchViewModel>();

            // Build a lookup of student's subject remarks by code (case-insensitive)
            var remarksMap = remarks
                .GroupBy(r => r.SubjectCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var targetSubject in targetProgram)
            {
                // Try to find matching subject in current program by:
                // 1. Exact code match
                // 2. Title match
                // 3. Normalized code match (e.g., PE1 vs PE-1)
                var matchedInCurrent = currentProgram.FirstOrDefault(s =>
                    s.Code.Equals(targetSubject.Code, StringComparison.OrdinalIgnoreCase) ||
                    s.Title.Equals(targetSubject.Title, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeSubjectCode(s.Code).Equals(NormalizeSubjectCode(targetSubject.Code), StringComparison.OrdinalIgnoreCase));

                // Look for remark using the CURRENT program's subject code if matched
                StudentSubjectRemarks? remark = null;
                bool isPassed = false;

                if (matchedInCurrent != null)
                {
                    // Try to find remark using the matched subject code from current program
                    if (remarksMap.TryGetValue(matchedInCurrent.Code, out remark))
                    {
                        isPassed = remark.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase);
                    }
                    // Fallback: try target subject code
                    else if (remarksMap.TryGetValue(targetSubject.Code, out remark))
                    {
                        isPassed = remark.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    // No match in current program, but check if there's a remark with the target code
                    // (this handles cases where subject codes are identical between programs)
                    if (remarksMap.TryGetValue(targetSubject.Code, out remark))
                    {
                        isPassed = remark.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase);
                        matchedInCurrent = targetSubject; // Treat as matched for credit purposes
                    }
                }

                matches.Add(new ShifterSubjectMatchViewModel
                {
                    SubjectCode = targetSubject.Code,
                    SubjectTitle = targetSubject.Title,
                    Units = targetSubject.Units,
                    IsMatched = matchedInCurrent != null,
                    IsPassed = isPassed,
                    IsCredited = matchedInCurrent != null && isPassed,
                    Remark = remark?.Remark ?? "Not Taken"
                });
            }

            return matches;
        }

        private string NormalizeSubjectCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;

            // Remove spaces, dashes, and convert to uppercase
            return code
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("_", "")
                .ToUpperInvariant()
                .Trim();
        }
        /// <summary>
        /// Check if student has any credited subjects from shifting
        /// </summary>
        private bool HasCreditedSubjects(
            string currentProgram,
            string targetProgram,
            List<StudentSubjectRemarks> remarks)
        {
            var currentSubjects = GetProgramSubjects(currentProgram, "1st Semester");
            var targetSubjects = GetProgramSubjects(targetProgram, "1st Semester");

            var creditedCount = targetSubjects.Count(targetSubject =>
            {
                var matchedInCurrent = currentSubjects.Any(s =>
                    s.Code.Equals(targetSubject.Code, StringComparison.OrdinalIgnoreCase) ||
                    s.Title.Equals(targetSubject.Title, StringComparison.OrdinalIgnoreCase));

                var remark = remarks.FirstOrDefault(r =>
                    r.SubjectCode.Equals(targetSubject.Code, StringComparison.OrdinalIgnoreCase));

                var isPassed = remark != null &&
                              remark.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase);

                return matchedInCurrent && isPassed;
            });

            return creditedCount > 0;
        }

        private List<string> GetCreditedSubjectCodes(
     string currentProgram,
     string targetProgram,
     List<StudentSubjectRemarks> remarks)
        {
            var currentSubjects = GetProgramSubjects(currentProgram, "1st Semester");
            var targetSubjects = GetProgramSubjects(targetProgram, "1st Semester");
            var credited = new List<string>();

            // Build remarks lookup
            var remarksMap = remarks
                .GroupBy(r => r.SubjectCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var targetSubject in targetSubjects)
            {
                // Find match using same logic as GetMatchedSubjects
                var matchedInCurrent = currentSubjects.FirstOrDefault(s =>
                    s.Code.Equals(targetSubject.Code, StringComparison.OrdinalIgnoreCase) ||
                    s.Title.Equals(targetSubject.Title, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeSubjectCode(s.Code).Equals(NormalizeSubjectCode(targetSubject.Code), StringComparison.OrdinalIgnoreCase));

                if (matchedInCurrent == null)
                {
                    // Check if remark exists with target code
                    if (remarksMap.ContainsKey(targetSubject.Code))
                    {
                        matchedInCurrent = targetSubject;
                    }
                    else
                    {
                        continue;
                    }
                }

                // Check if passed using current program's code
                StudentSubjectRemarks? remark = null;
                if (remarksMap.TryGetValue(matchedInCurrent.Code, out remark) ||
                    remarksMap.TryGetValue(targetSubject.Code, out remark))
                {
                    var isPassed = remark.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase);
                    if (isPassed)
                    {
                        credited.Add(targetSubject.Code);
                    }
                }
            }

            return credited;
        }

        [HttpGet]
        public async Task<IActionResult> RejectedShifter(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetShifterEnrollmentRequestsByStatusAsync("Rejected")
                : await SearchShifterRequestsByStatusAsync("Rejected", q, program);

            ViewBag.Title = "Rejected Shifter Requests";
            ViewBag.FilterAction = "RejectedShifter";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            ViewBag.EnrollmentSettings = await _db.GetEnrollmentSettingsAsync();

            return View("RejectedShifter", items);
        }
        [HttpGet]
        public async Task<IActionResult> AcceptedShifter(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetShifterEnrollmentRequestsByStatusAsync("Accepted")
                : await SearchShifterRequestsByStatusAsync("Accepted", q, program);

            ViewBag.Title = "Accepted Shifter Requests";
            ViewBag.FilterAction = "AcceptedShifter";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            ViewBag.EnrollmentSettings = await _db.GetEnrollmentSettingsAsync();

            return View("AcceptedShifter", items);
        }

        [HttpGet]
        public async Task<IActionResult> PendingShifter(string? q = null, string? program = null)
        {
            var items = (string.IsNullOrWhiteSpace(q) && string.IsNullOrWhiteSpace(program))
                ? await _db.GetShifterEnrollmentRequestsByStatusAsync("Pending")
                : await SearchShifterRequestsByStatusAsync("Pending", q, program);

            ViewBag.Title = "Pending Shifter Requests";
            ViewBag.FilterAction = "PendingShifter";
            ViewBag.FilterQ = q ?? "";
            ViewBag.FilterProgram = program ?? "";
            ViewBag.EnrollmentSettings = await _db.GetEnrollmentSettingsAsync();

            return View("PendingShifter", items);
        }


        public IActionResult AdminPosting()
        {
            return View("AdminPosting");
        }

    }
}