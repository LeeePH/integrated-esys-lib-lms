using Amazon.Runtime.Internal.Util;
using E_SysV0._01.Hubs;
using E_SysV0._01.Models;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]


    public class StudentAccountController : Controller
    {
        private readonly MongoDBServices _firebase;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<AdminNotificationsHub> _hub;
        private readonly EmailServices _email; 
        private readonly ILogger<StudentAccountController> _logger;
        private readonly IMemoryCache _cache;
        private class RateLimitEntry
        {
            public int Count { get; }
            public DateTime FirstRequestUtc { get; }
            public DateTime LastRequestUtc { get; }

            public RateLimitEntry(int count, DateTime firstRequestUtc, DateTime lastRequestUtc)
            {
                Count = count;
                FirstRequestUtc = firstRequestUtc;
                LastRequestUtc = lastRequestUtc;
            }
        }
        public StudentAccountController(MongoDBServices firebase, IWebHostEnvironment env, IHubContext<AdminNotificationsHub> hub, EmailServices email, ILogger<StudentAccountController> logger, IMemoryCache cache)
        {
            _firebase = firebase;
            _env = env;
            _hub = hub;
            _email = email;
            _logger = logger;
            _cache = cache;
        }

        private static readonly string[] ReqDocKeys = new[] { "Form138", "GoodMoral", "Diploma", "MedicalCertificate", "CertificateOfIndigency", "BirthCertificate" };
        private static bool FlagsAllSubmitted(Dictionary<string, string>? flags)
        {
            if (flags == null || flags.Count == 0) return false;
            foreach (var k in ReqDocKeys)
            {
                if (!flags.TryGetValue(k, out var v)) return false;
                if (!string.Equals((v ?? "").Trim(), "Submitted", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }
        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken] // keep or remove depending on your security decision
        public async Task<IActionResult> ForgotPassword(string email)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToLowerInvariant();
            var userMessage = "If an account exists for that email, a reset link has been sent.";

            // Rate-limit configuration (tunable)
            const int MaxRequestsPerHourPerEmail = 5;
            const int MaxRequestsPerHourPerIp = 20;
            var window = TimeSpan.FromHours(1);

            DateTime now = DateTime.UtcNow;

            string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var emailKey = $"rl:forgot:email:{emailNorm}";
            var ipKey = $"rl:forgot:ip:{ip}";


            // helper to increment and store
            RateLimitEntry IncrementEntry(string key, int max)
            {
                var ok = _cache.Get(key) as RateLimitEntry;
                if (ok == null || (now - ok.FirstRequestUtc) > window)
                {
                    var fresh = new RateLimitEntry(1, now, now);
                    _cache.Set(key, fresh, now.Add(window));
                    return fresh;
                }
                else
                {
                    var updated = new RateLimitEntry(ok.Count + 1, ok.FirstRequestUtc, now);
                    _cache.Set(key, updated, ok.FirstRequestUtc.Add(window));
                    return updated;
                }
            }

            try
            {
                // If email empty, respond generically (no rate-limit increment)
                if (string.IsNullOrWhiteSpace(emailNorm))
                {
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = true, message = userMessage });
                    TempData["Success"] = userMessage;
                    return RedirectToAction(nameof(StudentLogin));
                }

                // Check and increment per-email
                var eEntry = IncrementEntry(emailKey, MaxRequestsPerHourPerEmail);
                if (eEntry.Count > MaxRequestsPerHourPerEmail)
                {
                    var wait = (eEntry.FirstRequestUtc.Add(window) - now);
                    var minutes = Math.Ceiling(wait.TotalMinutes);
                    var msg = $"Too many password reset requests for that email. Try again in {minutes} minute(s).";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(StudentLogin));
                }

                // Check and increment per-IP
                var ipEntry = IncrementEntry(ipKey, MaxRequestsPerHourPerIp);
                if (ipEntry.Count > MaxRequestsPerHourPerIp)
                {
                    var wait = (ipEntry.FirstRequestUtc.Add(window) - now);
                    var minutes = Math.Ceiling(wait.TotalMinutes);
                    var msg = $"Too many requests from your network. Try again in {minutes} minute(s).";
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { success = false, message = msg });
                    TempData["Error"] = msg;
                    return RedirectToAction(nameof(StudentLogin));
                }

                // Proceed with existing behavior (generate token + send email)
                var student = await _firebase.GetStudentByEmailAsync(emailNorm);
                if (student != null)
                {
                    var tokenBytes = RandomNumberGenerator.GetBytes(32);
                    var token = WebEncoders.Base64UrlEncode(tokenBytes);

                    static string HashToken(string t)
                    {
                        var bytes = Encoding.UTF8.GetBytes(t);
                        var hash = SHA256.HashData(bytes);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }

                    student.ResetTokenHash = HashToken(token);
                    student.ResetTokenExpiryUtc = DateTime.UtcNow.AddMinutes(60);
                    await _firebase.UpdateStudentAsync(student);

                    var link = Url.Action("ResetPassword", "StudentAccount", new { area = "Student", email = emailNorm, token }, Request.Scheme);
                    try
                    {
                        await _email.SendPasswordResetEmailAsync(emailNorm, link);
                    }
                    catch (Exception exMail)
                    {
                        _logger?.LogWarning(exMail, "Failed to send reset email to {Email}", emailNorm);
                    }
                }

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true, message = userMessage });

                TempData["Success"] = userMessage;
                return RedirectToAction(nameof(StudentLogin));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ForgotPassword failure for {Email}", emailNorm);
                var friendly = "Failed to send reset link. Please try again.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = false, message = friendly });
                TempData["Error"] = friendly;
                return RedirectToAction(nameof(StudentLogin));
            }
        }

        // -------------------------
        // Reset password (via email link)
        // -------------------------
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string email, string token)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(emailNorm) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByEmailAsync(emailNorm);
            if (student == null || student.ResetTokenExpiryUtc == null || student.ResetTokenExpiryUtc < DateTime.UtcNow)
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(StudentLogin));
            }

            static string HashToken(string t)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(t);
                var hash = System.Security.Cryptography.SHA256.HashData(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            var providedHash = HashToken(token);
            if (!string.Equals(providedHash, student.ResetTokenHash ?? "", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(StudentLogin));
            }

            // Token valid -> render the login view directly (so antiforgery token on the page matches the form post)
            TempData["OpenResetPasswordModal"] = "1";
            ViewBag.ResetEmail = emailNorm;
            ViewBag.ResetToken = token;
            return View("~/Areas/Student/Views/Student/StudentLogin.cshtml");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string email, string token, string newPassword, string confirmPassword)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(emailNorm) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid request.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            // Server-side password policy
            var username = string.Empty;
            var student = await _firebase.GetStudentByEmailAsync(emailNorm);
            if (student == null)
            {
                // Don't reveal existence
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            username = student.Username;

            // Validate token again
            if (student.ResetTokenExpiryUtc == null || student.ResetTokenExpiryUtc < DateTime.UtcNow)
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            string HashToken(string t)
            {
                var bytes = Encoding.UTF8.GetBytes(t);
                var hash = SHA256.HashData(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
            var providedHash = HashToken(token);
            if (!string.Equals(providedHash, student.ResetTokenHash ?? "", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Invalid or expired reset link.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var policyError = ValidatePasswordPolicy(newPassword, username);
            if (policyError != null)
            {
                TempData["Error"] = policyError;
                // Re-show form with same token/email
                return RedirectToAction(nameof(ResetPassword), new { email = emailNorm, token });
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return RedirectToAction(nameof(ResetPassword), new { email = emailNorm, token });
            }

            // Persist new password and clear token
            student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            student.FirstLogin = false;
            student.ResetTokenHash = "";
            student.ResetTokenExpiryUtc = null;
            await _firebase.UpdateStudentAsync(student);

            // Sign in the user
            await HttpContext.SignOutAsync("StudentCookie");
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, student.Username),
                new Claim(ClaimTypes.Role, "Student")
            };
            var identity = new ClaimsIdentity(claims, "StudentCookie");
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync("StudentCookie", principal);

            TempData["Success"] = "Password updated. You are now signed in.";
            return RedirectToAction("DashboardPage", "Landing", new { area = "Student" });
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRegularEnrollment(
                   string EnrollmentType,
                   FreshmenInfoModel info)
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.LastName)) errors.Add("Student Last Name required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.FirstName)) errors.Add("Student First Name required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.Sex)) errors.Add("Student Sex required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.ContactNumber)) errors.Add("Student Contact Number required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.EmailAddress)) errors.Add("Student Email required.");
            if (string.IsNullOrWhiteSpace(info.StudentAddress.HouseStreet)) errors.Add("Student Address required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.LastName)) errors.Add("Guardian Last Name required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.FirstName)) errors.Add("Guardian First Name required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.Sex)) errors.Add("Guardian Sex required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.ContactNumber)) errors.Add("Guardian Contact Number required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.Relationship)) errors.Add("Guardian Relationship required.");

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", errors);
                return RedirectToAction(nameof(StudentDashboard));
            }

            var emailNorm = ((student.Email ?? info.StudentPersonal.EmailAddress) ?? string.Empty).Trim().ToLowerInvariant();
            var latest = await _firebase.GetLatestRequestByEmailAsync(emailNorm);
            if (latest != null && (latest.Status.EndsWith("Sem Pending", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = "A semester enrollment is already pending.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var settings = await _firebase.GetEnrollmentSettingsAsync();
            if (!settings.IsOpen)
            {
                TempData["Error"] = "Enrollment window is closed.";
                return RedirectToAction(nameof(StudentDashboard));
            }
            if (string.IsNullOrWhiteSpace(settings.AcademicYear))
            {
                TempData["Error"] = "Admin has not configured Academic Year yet.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ NEW: Check for pending library penalties
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var penalties = await _firebase.GetStudentPenaltiesFromLibraryAsync(student.Email);
                    var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                    if (pendingPenalties.Any())
                    {
                        var totalPending = pendingPenalties.Sum(p => p.Amount);
                        TempData["Error"] = $"Cannot proceed with enrollment: You have {pendingPenalties.Count} pending library penalty(ies) totaling ₱{totalPending:N2}. Please settle your penalties at the library counter before enrolling.";
                        return RedirectToAction(nameof(StudentDashboard));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SubmitRegularEnrollment] Error checking penalties: {ex.Message}");
                // Continue with enrollment if penalty check fails (non-blocking)
            }

            if (string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(student.Type, "Freshmen", StringComparison.OrdinalIgnoreCase))
            {
                var prior = latest;
                if (prior == null || !FlagsAllSubmitted(prior.DocumentFlags))
                {
                    TempData["Error"] = "Compliance required: complete all required documents (Submitted) before 2nd Semester enrollment.";
                    return RedirectToAction(nameof(StudentDashboard));
                }

                // Check for failed subjects and force irregular enrollment
                var previousRemarks = prior.ExtraFields?
                    .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                        kvp => kvp.Value,
                        StringComparer.OrdinalIgnoreCase
                    ) ?? new Dictionary<string, string>();

                bool hasFailedSubjects = previousRemarks.Any(r =>
                    r.Value.Equals("fail", StringComparison.OrdinalIgnoreCase));

                // Override enrollment type if student has failed subjects
                if (hasFailedSubjects && !string.Equals(EnrollmentType, "Irregular", StringComparison.OrdinalIgnoreCase))
                {
                    EnrollmentType = "Irregular";
                }
            }



            var semesterStatus = (settings.Semester?.StartsWith("2") ?? false) ? "2nd Sem Pending" : "1st Sem Pending";
            info.Academic ??= new AcademicInfo();
            info.Academic.AcademicYear = settings.AcademicYear;

            string Compose(string l, string f, string m, string? e)
                => string.Join(" ", new[] { f, m, l, e }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var fullName = Compose(info.StudentPersonal.LastName, info.StudentPersonal.FirstName,
                                   info.StudentPersonal.MiddleName, info.StudentPersonal.Extension);
            var emergency = Compose(info.GuardianPersonal.LastName, info.GuardianPersonal.FirstName,
                                    info.GuardianPersonal.MiddleName, info.GuardianPersonal.Extension);

            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Student.LastName"] = info.StudentPersonal.LastName,
                ["Student.FirstName"] = info.StudentPersonal.FirstName,
                ["Student.MiddleName"] = info.StudentPersonal.MiddleName,
                ["Student.Extension"] = info.StudentPersonal.Extension ?? "",
                ["Student.Sex"] = info.StudentPersonal.Sex,
                ["Student.ContactNumber"] = info.StudentPersonal.ContactNumber,
                ["Student.EmailAddress"] = emailNorm,
                ["StudentAddress.HouseStreet"] = info.StudentAddress.HouseStreet,
                ["StudentAddress.Barangay"] = info.StudentAddress.Barangay,
                ["StudentAddress.City"] = info.StudentAddress.City,
                ["StudentAddress.PostalCode"] = info.StudentAddress.PostalCode,
                ["Academic.Program"] = info.Academic.Program,
                ["Academic.YearLevel"] = info.Academic.YearLevel,
                ["Academic.Semester"] = settings.Semester,
                ["Academic.AcademicYear"] = settings.AcademicYear,
                ["Academic.EnrollmentType"] = EnrollmentType, // NEW: Store enrollment type
                ["Guardian.LastName"] = info.GuardianPersonal.LastName,
                ["Guardian.FirstName"] = info.GuardianPersonal.FirstName,
                ["Guardian.MiddleName"] = info.GuardianPersonal.MiddleName,
                ["Guardian.Extension"] = info.GuardianPersonal.Extension ?? "",
                ["Guardian.Sex"] = info.GuardianPersonal.Sex,
                ["Guardian.ContactNumber"] = info.GuardianPersonal.ContactNumber,
                ["Guardian.Relationship"] = info.GuardianPersonal.Relationship,
                ["GuardianAddress.HouseStreet"] = info.GuardianAddress.HouseStreet,
                ["GuardianAddress.Barangay"] = info.GuardianAddress.Barangay,
                ["GuardianAddress.City"] = info.GuardianAddress.City,
                ["GuardianAddress.PostalCode"] = info.GuardianAddress.PostalCode
            };

            string requestType;
            if (string.Equals(student.Type, "Freshmen", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.Semester?.StartsWith("2") ?? false)
                {
                    // For 2nd semester, append enrollment type suffix
                    requestType = string.Equals(EnrollmentType, "Irregular", StringComparison.OrdinalIgnoreCase)
                        ? "Freshmen-Irregular"
                        : "Freshmen-Regular";
                }
                else
                {
                    requestType = "Freshmen";
                }
            }
            else
            {
                requestType = string.Equals(EnrollmentType, "Irregular", StringComparison.OrdinalIgnoreCase)
                    ? "Irregular"
                    : "Regular";
            }

            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = emailNorm,
                FullName = fullName,
                Program = string.IsNullOrWhiteSpace(info.Academic.Program) ? "BSIT" : info.Academic.Program.Trim(),
                Type = requestType,
                Status = semesterStatus,
                SubmittedAt = DateTime.UtcNow,
                EmergencyContactName = emergency,
                EmergencyContactPhone = info.GuardianPersonal.ContactNumber,
                ExtraFields = extra
            };

            if (semesterStatus == "2nd Sem Pending")
            {
                var previousRemarks = latest?.ExtraFields?
                    .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                        kvp => kvp.Value,
                        StringComparer.OrdinalIgnoreCase
                    ) ?? new Dictionary<string, string>();

                // Determine program to use for eligibility computation (prefer latest request program)
                var programForCalc = latest?.Program ?? (latest?.ExtraFields != null && latest.ExtraFields.TryGetValue("Academic.Program", out var lp) ? lp : request.Program ?? "BSIT");
                programForCalc = NormalizeProgramCode(programForCalc);

                var eligibility = CalculateSecondSemesterEligibility(previousRemarks, programForCalc);
                request.SecondSemesterEligibility = eligibility;
            }

            await _firebase.SubmitEnrollmentRequestAsync(request);
            try
            {
                var link = Url.Action("RequestDetails", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                {
                    type = "PendingSubmitted",
                    title = "New pending enrollment",
                    message = $"{request.FullName} submitted ({semesterStatus}) for {request.Program}.",
                    severity = "info",
                    icon = "hourglass-half",
                    id = request.Id,
                    link, // <-- direct to RequestDetails
                    email = request.Email,
                    program = request.Program,
                    status = semesterStatus,
                    academicYear = settings.AcademicYear,
                    submittedAt = request.SubmittedAt
                });
            }
            catch { /* non-fatal */ }

            TempData["Success"] = $"Semester enrollment submitted ({semesterStatus}).";
            return RedirectToAction(nameof(StudentDashboard));
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> RegistrationSlip()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null) return BadRequest("Student not found.");

            var req = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (req is null) return BadRequest("No enrollment request found.");

            var sse = await _firebase.GetStudentSectionEnrollmentAsync(student.Username);
            if (sse is null) return BadRequest("No section enrollment found.");

            var section = await _firebase.GetSectionByIdAsync(sse.SectionId);
            var meetings = await _firebase.GetStudentScheduleAsync(student.Username);
            var roomNames = await _firebase.GetRoomNamesByIdsAsync(meetings.Select(m => m.RoomId));

            // Build subject lookup (mirror other controllers/services)
            var subjectDict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

            // determine canonical program from section or request
            var program = NormalizeProgramCode(section?.Program ?? (req.Program ?? ""));
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

            var subjects = new List<AdminRegistrationSlipSubject>();
            foreach (var m in meetings)
            {
                var code = m.CourseCode ?? "";
                subjectDict.TryGetValue(code, out var meta);
                subjects.Add(new AdminRegistrationSlipSubject
                {
                    Code = code,
                    Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                    Units = meta.Units,
                    Room = roomNames.TryGetValue(m.RoomId ?? "", out var rn) ? rn : (m.RoomId ?? ""),
                    Schedule = $"{DayName(m.DayOfWeek)} {(string.IsNullOrWhiteSpace(m.DisplayTime) ? "" : m.DisplayTime)}".Trim()
                });
            }

            var extra = req.ExtraFields ?? new Dictionary<string, string>();
            string G(string p, string n)
            {
                var key = string.IsNullOrEmpty(p) ? n : $"{p}.{n}";
                return extra.TryGetValue(key, out var v) ? v : "";
            }

            bool isIrregular = (req.Type != null &&
                                (req.Type.Contains("Irregular", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(req.Type, "Transferee", StringComparison.OrdinalIgnoreCase)));

            var vm = new AdminRegistrationSlipViewModel
            {
                Program = section?.Program ?? (req.Program ?? ""),
                YearLevel = G("Academic", "YearLevel"),
                Semester = G("Academic", "Semester"),
                SectionName = section?.Name ?? "",
                Regularity = isIrregular ? "Irregular" : "Regular",
                GraduatingStatus = "Not Graduating",
                LastName = G("Student", "LastName"),
                FirstName = G("Student", "FirstName"),
                MiddleName = G("Student", "MiddleName"),
                DateEnrolledUtc = sse.EnrolledAt,
                Subjects = subjects,
                DeanName = "Engr. Juan Dela Cruz",
                RequestId = req.Id
            };

            return PartialView("~/Areas/Admin/Views/Admin/_RegistrationSlipModal.cshtml", vm);
        }



        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> StudentDashboard()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            EnrollmentRequest? enrollment = null;
            List<EnrollmentRequest> history = new();
            if (!string.IsNullOrWhiteSpace(student.Email))
            {
                enrollment = await _firebase.GetLatestRequestByEmailAsync(student.Email);
                history = await _firebase.GetRequestsByEmailAsync(student.Email) ?? new List<EnrollmentRequest>();
            }

            var schedule = (await _firebase.GetStudentScheduleAsync(student.Username)) ?? new List<ClassMeeting>();
            var settings = (await _firebase.GetEnrollmentSettingsAsync()) ?? new EnrollmentSettings
            {
                IsOpen = false,
                Semester = "1st Semester",
                ProgramCapacities = new Dictionary<string, int>()
            };

            var vm = new StudentDashboardViewModel
            {
                StudentId = student.Id,
                Username = student.Username,
                Email = student.Email,
                Type = student.Type,
                FirstLogin = student.FirstLogin,
                Enrollment = enrollment,
                SectionId = schedule.Count > 0 ? schedule[0].SectionId : null,
                Schedule = schedule,
                EnrollmentOpen = settings.IsOpen,
                EnrollmentSemester = settings.Semester,
                EnrollmentAcademicYear = settings.AcademicYear,
                EnrollmentHistory = history
            };

            // ✅ CRITICAL FIX: Collect ALL historical subject remarks from ALL sources
            var allSubjectRemarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);



            try
            {
                // Priority 1: Get from MongoDB student_subject_remarks collection (most authoritative)
                var mongoRemarks = await _firebase.GetStudentSubjectRemarksAsync(student.Username);
                if (mongoRemarks != null && mongoRemarks.Any())
                {
                    foreach (var remark in mongoRemarks)
                    {
                        if (!string.IsNullOrWhiteSpace(remark.SubjectCode))
                        {
                            allSubjectRemarks[remark.SubjectCode] = remark.Remark ?? "ongoing";
                        }
                    }
                    Console.WriteLine($"[StudentDashboard] Loaded {mongoRemarks.Count} remarks from MongoDB");
                }

                // Priority 2: Extract from current enrollment using helper (includes archives)
                if (enrollment != null)
                {
                    var extractedRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(enrollment, _firebase);
                    foreach (var kvp in extractedRemarks)
                    {
                        // Don't overwrite MongoDB remarks (they're more authoritative)
                        if (!allSubjectRemarks.ContainsKey(kvp.Key))
                        {
                            allSubjectRemarks[kvp.Key] = kvp.Value;
                        }
                    }
                    Console.WriteLine($"[StudentDashboard] Extracted {extractedRemarks.Count} remarks from ExtractAllFirstYearRemarksAsync");
                }

                // Priority 3: Get from current enrollment ExtraFields (fallback)
                if (enrollment?.ExtraFields != null)
                {
                    foreach (var kvp in enrollment.ExtraFields)
                    {
                        if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                        {
                            var code = kvp.Key.Substring("SubjectRemarks.".Length)
                                             .Replace("2ndSem.", "")
                                             .Replace("1stSem.", "")
                                             .Trim();

                            if (!string.IsNullOrWhiteSpace(code) && !allSubjectRemarks.ContainsKey(code))
                            {
                                allSubjectRemarks[code] = kvp.Value;
                            }
                        }
                    }
                }

                // Priority 4: Get from archived records (if any)
                if (!string.IsNullOrWhiteSpace(student.Email) && !string.IsNullOrWhiteSpace(settings.AcademicYear))
                {
                    var archived = await _firebase.GetArchivedFirstSemesterEnrollmentAsync(student.Email, settings.AcademicYear);
                    if (archived?.ExtraFields != null)
                    {
                        foreach (var kvp in archived.ExtraFields)
                        {
                            if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                            {
                                var code = kvp.Key.Substring("SubjectRemarks.".Length).Trim();
                                if (!string.IsNullOrWhiteSpace(code) && !allSubjectRemarks.ContainsKey(code))
                                {
                                    allSubjectRemarks[code] = kvp.Value;
                                }
                            }
                        }
                        Console.WriteLine($"[StudentDashboard] Loaded {archived.ExtraFields.Count(kvp => kvp.Key.StartsWith("SubjectRemarks."))} remarks from archives");
                    }
                }

                Console.WriteLine($"[StudentDashboard] Total remarks collected: {allSubjectRemarks.Count}");

                try
                {
                    if (schedule != null && schedule.Any())
                    {
                        foreach (var meeting in schedule)
                        {
                            var code = (meeting?.CourseCode ?? "").Trim();
                            if (!string.IsNullOrWhiteSpace(code) && !allSubjectRemarks.ContainsKey(code))
                            {
                                allSubjectRemarks[code] = "ongoing";
                                Console.WriteLine($"[StudentDashboard] Marking scheduled subject as ongoing: {code}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StudentDashboard] Warning: failed to merge schedule into remarks: {ex.Message}");
                }

                // ✅ NEW: Use CurriculumValidator to check if student completed all required subjects
                if (enrollment != null && allSubjectRemarks.Any())
                {
                    var completedYearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year");
                    var completedSemester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester");
                    var program = NormalizeProgramCode(enrollment.Program ?? "BSIT");

                    var validation = CurriculumValidator.ValidateCurriculumCompletion(
                        program,
                        completedYearLevel,
                        completedSemester,
                        allSubjectRemarks
                    );

                    // Student is irregular if:
                    // 1. Missing any required subjects from curriculum, OR
                    // 2. Failed any subjects (even if retaken and passed later)
                    vm.HasFailedSubjects = !validation.isRegular;
                    vm.MissingSubjects = validation.missingSubjects;
                    vm.FailedSubjects = validation.failedSubjects;

                    Console.WriteLine($"[StudentDashboard] Curriculum validation complete:");
                    Console.WriteLine($"  - IsRegular: {validation.isRegular}");
                    Console.WriteLine($"  - Missing subjects: {string.Join(", ", validation.missingSubjects)}");
                    Console.WriteLine($"  - Failed subjects: {string.Join(", ", validation.failedSubjects)}");
                }
                else
                {
                    vm.HasFailedSubjects = false;
                    vm.MissingSubjects = new List<string>();
                    vm.FailedSubjects = new List<string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentDashboard] Error loading historical remarks: {ex.Message}");
                // Continue with empty remarks rather than crashing
                vm.HasFailedSubjects = false;
            }

            // Store all remarks in view model for display
            vm.SubjectRemarks = allSubjectRemarks;

            // ✅ Check for 2nd Year enrollment eligibility
            if (enrollment != null)
            {
                var currentYearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");
                var currentSemester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "");
                var isEnrolled = enrollment.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase);

                if (isEnrolled &&
                    currentYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase) &&
                    currentSemester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase) &&
                    settings.IsOpen &&
                    settings.Semester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase))
                {
                    vm.CanEnrollFor2ndYear = true;
                    vm.TargetYearLevel = "2nd Year";
                    vm.TargetSemester = "1st Semester";

                    var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(enrollment, _firebase);
                    vm.CombinedFirstYearRemarks = allRemarks;

                    Console.WriteLine($"[StudentDashboard] Extracted {allRemarks.Count} combined remarks for {student.Username}");

                    vm.SecondYearEnrollmentType = SecondYearEnrollmentHelper.DetermineSecondYearEnrollmentType(allRemarks);

                    var program = NormalizeProgramCode(enrollment.Program ?? "BSIT");
                    var secondYearSubjects = GetSubjectsForYearAndSemester(program, "2nd Year", "1st Semester");
                    var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);

                    vm.SecondYearSubjects = SecondYearEnrollmentHelper.Calculate2ndYearEligibility(
                        secondYearSubjects,
                        allRemarks,
                        prereqMap);

                    Console.WriteLine($"[StudentDashboard] Calculated eligibility for {vm.SecondYearSubjects.Count} 2nd year subjects");
                }
            }

            // Build subject map
            var programForLookup = NormalizeProgramCode(enrollment?.Program ?? "BSIT");
            var subjMap = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(programForLookup, "BSENT", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                    subjMap[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                    subjMap[s.Code] = (s.Title, s.Units);
            }
            else
            {
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                    subjMap[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                    subjMap[s.Code] = (s.Title, s.Units);
            }

            vm.SubjectSchedules = schedule.Select(m =>
            {
                var code = m.CourseCode ?? string.Empty;
                subjMap.TryGetValue(code, out var meta);
                return new StudentSubjectScheduleRow
                {
                    Code = code,
                    Title = string.IsNullOrWhiteSpace(meta.Title) ? code : meta.Title,
                    Units = meta.Units,
                    DayOfWeek = m.DayOfWeek,
                    DisplayTime = string.IsNullOrWhiteSpace(m.DisplayTime) ? "-" : m.DisplayTime,
                    RoomId = m.RoomId
                };
            }).OrderBy(r => r.DayOfWeek).ThenBy(r => r.DisplayTime).ToList();

            // Handle 2nd semester logic
            var ef = enrollment?.ExtraFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var semFromReq = ef.TryGetValue("Academic.Semester", out var es) ? es : settings.Semester;
            bool enrolledNow = (enrollment?.Status ?? "").StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase);
            bool secondSemPending = (enrollment?.Status ?? "").Equals("2nd Sem Pending", StringComparison.OrdinalIgnoreCase);
            bool secondSemEnrolled = enrolledNow && string.Equals(semFromReq, "2nd Semester", StringComparison.OrdinalIgnoreCase);

            if (secondSemPending || secondSemEnrolled)
            {
                vm.ShowSecondSemesterSubjects = true;

                if (enrollment?.SecondSemesterEligibility != null && enrollment.SecondSemesterEligibility.Any())
                {
                    vm.SecondSemesterEligibility = enrollment.SecondSemesterEligibility;
                }
                else
                {
                    var prog = NormalizeProgramCode(enrollment?.Program ?? "BSIT");
                    vm.SecondSemesterEligibility = CalculateSecondSemesterEligibility(allSubjectRemarks, prog);
                }

                if (secondSemEnrolled && schedule.Count > 0)
                {
                    vm.ShowSecondSemesterSubjects = false;
                }
                else
                {
                    var prog = NormalizeProgramCode(enrollment?.Program ?? "BSIT");
                    if (string.Equals(prog, "BSENT", StringComparison.OrdinalIgnoreCase))
                    {
                        vm.SecondSemesterSubjects = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects
                            .Select(s => new StudentSubjectScheduleRow
                            {
                                Code = s.Code,
                                Title = s.Title,
                                Units = s.Units,
                                DayOfWeek = 0,
                                DisplayTime = "To be assigned",
                                RoomId = "To be assigned"
                            }).ToList();
                    }
                    else
                    {
                        vm.SecondSemesterSubjects = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects
                            .Select(s => new StudentSubjectScheduleRow
                            {
                                Code = s.Code,
                                Title = s.Title,
                                Units = s.Units,
                                DayOfWeek = 0,
                                DisplayTime = "To be assigned",
                                RoomId = "To be assigned"
                            }).ToList();
                    }
                }

                // Load previous subjects (1st semester)
                vm.PreviousSubjectRemarks = allSubjectRemarks
                    .Where(kvp =>
                    {
                        // Filter for 1st semester subjects only
                        var is1stSem = (string.Equals(programForLookup, "BSENT", StringComparison.OrdinalIgnoreCase)
                            ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                            : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                            .Any(s => s.Code.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
                        return is1stSem;
                    })
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                var prevProg = NormalizeProgramCode(enrollment?.Program ?? "BSIT");
                if (string.Equals(prevProg, "BSENT", StringComparison.OrdinalIgnoreCase))
                {
                    vm.PreviousSubjects = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                        .Select(s => new StudentSubjectRow
                        {
                            Code = s.Code,
                            Title = s.Title,
                            Units = s.Units
                        })
                        .ToList();
                }
                else
                {
                    vm.PreviousSubjects = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects
                        .Select(s => new StudentSubjectRow
                        {
                            Code = s.Code,
                            Title = s.Title,
                            Units = s.Units
                        })
                        .ToList();
                }
            }
            // If student is already advanced to 2nd Year 1st Semester, ensure the dashboard shows 1st year 1st-semester subjects + remarks
            if (enrollment != null)
            {
                var enrolledYearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "") ?? "";
                var enrolledSemester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "") ?? "";

                if (enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                    enrolledSemester.Contains("1st", StringComparison.OrdinalIgnoreCase))
                {
                    var program = NormalizeProgramCode(enrollment.Program ?? "BSIT");

                    // canonical 1st Year 1st Sem subject list for the program
                    var prevSubjectsList = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                        ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                        : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects;

                    // get ALL first-year remarks (MongoDB + ExtraFields + archives)
                    var allFirstYearRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(enrollment, _firebase);

                    vm.PreviousSubjects = prevSubjectsList
                        .Select(s => new StudentSubjectRow { Code = s.Code, Title = s.Title, Units = s.Units })
                        .ToList();

                    vm.PreviousSubjectRemarks = allFirstYearRemarks
                        .Where(kvp => vm.PreviousSubjects.Any(ps => ps.Code.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase)))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

                    Console.WriteLine($"[StudentDashboard] Populated PreviousSubjects for 2nd Year 1st Sem: {vm.PreviousSubjects.Count} items, remarks: {vm.PreviousSubjectRemarks.Count}");
                }
            }

            // ✅ NEW: Fetch penalties from library system
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var penalties = await _firebase.GetStudentPenaltiesFromLibraryAsync(student.Email);
                    vm.Penalties = penalties;
                    vm.PendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                    vm.TotalPendingPenalties = vm.PendingPenalties.Sum(p => p.Amount);
                    vm.HasPendingPenalties = vm.PendingPenalties.Any();
                    
                    Console.WriteLine($"[StudentDashboard] Loaded {penalties.Count} penalties, {vm.PendingPenalties.Count} pending, Total: ₱{vm.TotalPendingPenalties:F2}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StudentDashboard] Error loading penalties: {ex.Message}");
                // Continue without penalties rather than crashing
                vm.Penalties = new List<Penalty>();
                vm.PendingPenalties = new List<Penalty>();
            }

            return View("~/Areas/Student/Views/Student/StudentDashboard.cshtml", vm);
        }

        private Dictionary<string, (string Title, int Units)> BuildSubjectDictionary(string program)
        {
            var dict = new Dictionary<string, (string Title, int Units)>(StringComparer.OrdinalIgnoreCase);
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            if (isBSENT)
            {
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
            }
            else
            {
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
                foreach (var s in E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects)
                    dict[s.Code] = (s.Title, s.Units);
            }

            return dict;
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> SubjectSelection2ndYear()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student not found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var enrollment = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (enrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var program = enrollment.ExtraFields?.GetValueOrDefault("Academic.Program", "BSIT") ?? "BSIT";

            // ✅ NEW: Detect current year level and semester to determine target
            var currentYearLevel = enrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year");
            var currentSemester = enrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester");

            // ✅ Determine target year/semester based on current enrollment
            string targetYearLevel;
            string targetSemester;
            List<SubjectRow> secondYearSubjects;

            bool is1stYearCompleted = currentYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase) &&
                                      currentSemester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase);

            bool is2ndYear1stSemCompleted = currentYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                            currentSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            if (is1stYearCompleted)
            {
                // Student completed 1st Year 2nd Sem → Enroll for 2nd Year 1st Sem
                targetYearLevel = "2nd Year";
                targetSemester = "1st Semester";
                secondYearSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList();
            }
            else if (is2ndYear1stSemCompleted)
            {
                // Student completed 2nd Year 1st Sem → Enroll for 2nd Year 2nd Sem
                targetYearLevel = "2nd Year";
                targetSemester = "2nd Semester";
                secondYearSubjects = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList();
            }
            else
            {
                TempData["Error"] = "You are not eligible for 2nd year subject selection at this time.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // Get all 1st Year remarks (both semesters)
            var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(enrollment, _firebase);

            // ✅ FIX: Only validate subjects the student actually took (has remarks for)
            var hasOngoingSubjects = allRemarks.Any(kv =>
                string.Equals(kv.Value, "ongoing", StringComparison.OrdinalIgnoreCase));

            if (hasOngoingSubjects)
            {
                TempData["Error"] = "Cannot proceed: some subjects are still marked Ongoing. Please wait for administrators to update subject remarks.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);
            var eligibilityList = SecondYearEnrollmentHelper.Calculate2ndYearEligibility(
                secondYearSubjects,
                allRemarks,
                prereqMap);

            // Build subject metadata
            var subjectTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var subjectSemesters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var firstYearSubjects = new List<SubjectRow>();
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            if (isBSENT)
            {
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects);
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects);
            }
            else
            {
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects);
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects);
            }

            foreach (var subject in firstYearSubjects)
            {
                subjectTitles[subject.Code] = subject.Title;
                var is1stSem = (isBSENT
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                    .Any(s => s.Code.Equals(subject.Code, StringComparison.OrdinalIgnoreCase));

                subjectSemesters[subject.Code] = is1stSem ? "1st Semester" : "2nd Semester";
            }

            var eligibilityStatus = eligibilityList.ToDictionary(
                s => s.Code,
                s => s.IsEligible ? "Can enroll - All prerequisites passed" : s.EligibilityReason,
                StringComparer.OrdinalIgnoreCase);

            var model = new SubjectSelection2ndYearViewModel
            {
                StudentUsername = student.Username,
                StudentName = enrollment.FullName ?? student.Username,
                Program = program,
                YearLevel = targetYearLevel, // ✅ Dynamic
                Semester = targetSemester,    // ✅ Dynamic
                Subjects = eligibilityList,
                MaxUnits = 24,
                FirstYearRemarks = allRemarks,
                AvailableSubjects = secondYearSubjects,
                EligibilityStatus = eligibilityStatus,
                SubjectTitles = subjectTitles,
                SubjectSemesters = subjectSemesters
            };

            // ✅ NEW: Get retake opportunities with DYNAMIC target semester
            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(program);

            var retakeOpportunities = SubjectRetakeHelper.GetRetakeOpportunities(
                studentRemarks: allRemarks,
                currentSemester: targetSemester, // ✅ NOW DYNAMIC!
                studentYearLevel: targetYearLevel, // ✅ NOW DYNAMIC!
                program: program,
                allSubjects: allProgramSubjects
            );

            Console.WriteLine($"[SubjectSelection2ndYear] Target: {targetYearLevel} {targetSemester}");
            Console.WriteLine($"[SubjectSelection2ndYear] Retake opportunities found: {retakeOpportunities.Count}");

            // ✅ Merge retake eligibility
            foreach (var retake in retakeOpportunities)
            {
                if (retake.IsEligible)
                {
                    eligibilityStatus[retake.Code] = "Can enroll (Retake)";
                }
                else
                {
                    eligibilityStatus[retake.Code] = $"Cannot retake: {retake.IneligibilityReason}";
                }
            }

            ViewBag.RetakeOpportunities = retakeOpportunities;

            return View("~/Areas/Student/Views/Student/SubjectSelection2ndYear.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> SelectSubjects2ndYear()
        {
            var username = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
            {
                return RedirectToAction("StudentLogin");
            }

            var student = await _firebase.GetStudentByUsernameAsync(username); // ✅ FIXED: _firebase
            if (student == null)
            {
                return RedirectToAction("StudentLogin");
            }

            // Get latest enrollment request
            var enrollment = await _firebase.GetLatestRequestByEmailAsync(student.Email); // ✅ FIXED
            if (enrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction("StudentDashboard");
            }

            // Verify eligibility for 2nd Year
            var settings = await _firebase.GetEnrollmentSettingsAsync(); // ✅ FIXED
            if (!EnrollmentRules.IsEligibleForNextYearEnrollment(enrollment, settings))
            {
                TempData["Error"] = "You are not yet eligible for 2nd Year enrollment.";
                return RedirectToAction("StudentDashboard");
            }

            // Check if eligibility was calculated
            if (enrollment.SecondSemesterEligibility == null || !enrollment.SecondSemesterEligibility.Any())
            {
                TempData["Error"] = "2nd Year eligibility has not been calculated yet. Please contact the registrar.";
                return RedirectToAction("StudentDashboard");
            }

            // Get program
            var program = enrollment.ExtraFields?.GetValueOrDefault("Academic.Program", enrollment.Program) ?? "BSIT";

            // Get all 1st Year remarks
            var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(enrollment, _firebase);
            // Check regularity
            var regularity = EnrollmentRules.DetermineRegularity(allRemarks);
            var isIrregular = regularity.Equals("Irregular", StringComparison.OrdinalIgnoreCase);

            if (!isIrregular)
            {
                TempData["Info"] = "You are a regular student. All 2nd Year subjects will be assigned automatically.";
                return RedirectToAction("Submit2ndYearEnrollment"); // Direct submission
            }

            // Get 2nd Year subjects
            var secondYearSubjects = GetSubjectsForYearAndSemester(program, "2nd Year", "1st Semester");

            // Build subject metadata
            var subjectTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var subjectSemesters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Get all 1st Year subjects for title/semester mapping
            var firstYearSubjects = new List<SubjectRow>();
            if (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase))
            {
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects);
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects);
            }
            else
            {
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects);
                firstYearSubjects.AddRange(E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects);
            }

            foreach (var subject in firstYearSubjects)
            {
                subjectTitles[subject.Code] = subject.Title;

                // Determine semester based on subject models
                var is1stSem = (string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear1stSem.Subjects
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear1stSem.Subjects)
                    .Any(s => s.Code.Equals(subject.Code, StringComparison.OrdinalIgnoreCase));

                subjectSemesters[subject.Code] = is1stSem ? "1st Semester" : "2nd Semester";
            }

            var vm = new SubjectSelection2ndYearViewModel
            {
                StudentUsername = student.Username,
                StudentName = student.Username, // Or fetch full name
                Program = program,
                FirstYearRemarks = allRemarks,
                AvailableSubjects = secondYearSubjects,
                EligibilityStatus = enrollment.SecondSemesterEligibility,
                SubjectTitles = subjectTitles,
                SubjectSemesters = subjectSemesters
            };

            return View("SubjectSelection2ndYear", vm);
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit2ndYearEnrollmentWithSubjects(
            string studentUsername,
            [FromForm] string[] selectedSubjects)
        {
            if (string.IsNullOrWhiteSpace(studentUsername))
            {
                TempData["Error"] = "Invalid student username.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            if (selectedSubjects == null || !selectedSubjects.Any())
            {
                TempData["Error"] = "You must select at least one subject to enroll.";
                return RedirectToAction(nameof(SubjectSelection2ndYear));
            }

            var student = await _firebase.GetStudentByUsernameAsync(studentUsername);
            if (student == null)
            {
                TempData["Error"] = "Student account not found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var currentEnrollment = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (currentEnrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var currentYearLevel = currentEnrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");
            var currentSemester = currentEnrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "");

            // ✅ Allow BOTH scenarios:
            // 1. 1st Year 2nd Semester → 2nd Year 1st Semester
            // 2. 2nd Year 1st Semester → 2nd Year 2nd Semester
            bool isValid1stYearToSecondYear = currentYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase) &&
                                              currentSemester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase);

            bool isValid2ndYearFirstToSecond = currentYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                               currentSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            if (!isValid1stYearToSecondYear && !isValid2ndYearFirstToSecond)
            {
                TempData["Error"] = "2nd Year enrollment is only available for students who completed 1st Year 2nd Semester OR 2nd Year 1st Semester.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // Determine target year/semester based on current enrollment
            string targetYear, targetSem;
            if (isValid1stYearToSecondYear)
            {
                targetYear = "2nd Year";
                targetSem = "1st Semester";
            }
            else // isValid2ndYearFirstToSecond
            {
                targetYear = "2nd Year";
                targetSem = "2nd Semester";
            }

            var settings = await _firebase.GetEnrollmentSettingsAsync();

            // ✅ Validate enrollment window matches target semester
            if (!settings.IsOpen)
            {
                TempData["Error"] = "Enrollment window is closed.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ Check if admin opened the correct semester
            bool correctSemesterOpen = (targetSem.Contains("1st") && settings.Semester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase)) ||
                                       (targetSem.Contains("2nd") && settings.Semester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase));

            if (!correctSemesterOpen)
            {
                TempData["Error"] = $"Enrollment for {targetYear} {targetSem} is not currently open.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ NEW: Check for pending library penalties
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var penalties = await _firebase.GetStudentPenaltiesFromLibraryAsync(student.Email);
                    var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                    if (pendingPenalties.Any())
                    {
                        var totalPending = pendingPenalties.Sum(p => p.Amount);
                        TempData["Error"] = $"Cannot proceed with enrollment: You have {pendingPenalties.Count} pending library penalty(ies) totaling ₱{totalPending:N2}. Please settle your penalties at the library counter before enrolling.";
                        return RedirectToAction(nameof(StudentDashboard));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Error checking penalties: {ex.Message}");
                // Continue with enrollment if penalty check fails (non-blocking)
            }

            var program = currentEnrollment.Program ?? "BSIT";

            // ✅ Get all 1st Year remarks (both semesters)
            var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(currentEnrollment, _firebase);

            // ✅ Check for failed subjects to determine regularity
            var hasFailedSubjects = allRemarks.Any(r =>
                string.Equals(r.Value, "fail", StringComparison.OrdinalIgnoreCase));

            // ✅ Use EnrollmentRules to determine regularity
            var regularity = EnrollmentRules.DetermineRegularity(allRemarks);
            var isIrregular = regularity.Equals("Irregular", StringComparison.OrdinalIgnoreCase) || hasFailedSubjects;

            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Student: {student.Username}");
            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] All remarks count: {allRemarks.Count}");
            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Has failed subjects: {hasFailedSubjects}");
            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Regularity: {regularity}");
            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Is Irregular: {isIrregular}");

            // ✅ CRITICAL FIX: Get all program subjects and retake opportunities FIRST (before using them)
            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(program);

            var retakeOpps = SubjectRetakeHelper.GetRetakeOpportunities(
                allRemarks,
                targetSem, // ✅ Use dynamic target semester
                targetYear, // ✅ Use dynamic target year
                program,
                allProgramSubjects
            );

            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Retake opportunities: {retakeOpps.Count}");

            // Get 2nd Year subjects (for eligibility calculation)
            var secondYearSubjects = GetSubjectsForYearAndSemester(program, targetYear, targetSem);

            // Get prerequisites
            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);

            // Calculate eligibility for 2nd year subjects
            var eligibilityList = SecondYearEnrollmentHelper.Calculate2ndYearEligibility(
                secondYearSubjects,
                allRemarks,
                prereqMap);

            // ✅ NOW: Build eligible subject codes from BOTH 2nd year subjects AND retakes
            var eligibleSubjectCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add 2nd year eligible subjects
            eligibleSubjectCodes.UnionWith(
                eligibilityList.Where(s => s.IsEligible).Select(s => s.Code)
            );

            // ✅ Add eligible retake subjects
            var retakeCodes = retakeOpps
                .Where(r => r.IsEligible)
                .Select(r => r.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase); // ✅ Convert to HashSet for faster lookups

            eligibleSubjectCodes.UnionWith(retakeCodes);

            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Eligible codes (including retakes): {string.Join(", ", eligibleSubjectCodes)}");
            Console.WriteLine($"[Submit2ndYearEnrollmentWithSubjects] Retake codes: {string.Join(", ", retakeCodes)}");

            // ✅ Validation includes both 2nd year subjects AND retakes
            var invalidSelections = selectedSubjects
                .Where(code => !eligibleSubjectCodes.Contains(code))
                .ToList();

            if (invalidSelections.Any())
            {
                TempData["Error"] = $"Cannot enroll in ineligible subjects: {string.Join(", ", invalidSelections)}";
                return RedirectToAction(nameof(SubjectSelection2ndYear));
            }

            // ✅ Validate selection (blocks concurrent prerequisite conflicts)
            var validation = SubjectRetakeHelper.ValidateSubjectSelection(
                selectedSubjects.ToList(),
                allProgramSubjects,
                allRemarks
            );

            if (!validation.isValid)
            {
                TempData["Error"] = string.Join("<br/>", validation.errors);
                return RedirectToAction(nameof(SubjectSelection2ndYear));
            }

            // ✅ Calculate unit load
            var unitLoad = SubjectRetakeHelper.CalculateUnitLoad(
                selectedSubjects.ToList(),
                allProgramSubjects,
                retakeOpps,
                maxUnits: 24
            );

            if (unitLoad.exceedsLimit)
            {
                TempData["Warning"] = $"⚠️ Total unit load ({unitLoad.totalUnits} units) exceeds recommended maximum (24 units).";
            }

            // ✅ Set correct enrollment type based on failed subjects
            var enrollmentType = isIrregular ? "2nd Year-Irregular" : "2nd Year-Regular";

            // Create 2nd Year enrollment request
            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = student.Email,
                FullName = currentEnrollment.FullName,
                Program = program,
                Type = enrollmentType,
                Status = targetSem.Contains("1st") ? "1st Sem Pending" : "2nd Sem Pending", // ✅ Dynamic status
                SubmittedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                EmergencyContactName = currentEnrollment.EmergencyContactName,
                EmergencyContactPhone = currentEnrollment.EmergencyContactPhone,
                DocumentFlags = currentEnrollment.DocumentFlags,
                ExtraFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            // Copy relevant fields
            if (currentEnrollment.ExtraFields != null)
            {
                foreach (var kvp in currentEnrollment.ExtraFields)
                {
                    if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    {
                        request.ExtraFields[kvp.Key] = kvp.Value;
                    }
                    else if (!kvp.Key.StartsWith("SecondSemesterEligibility", StringComparison.OrdinalIgnoreCase))
                    {
                        request.ExtraFields[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Update with new academic info
            request.ExtraFields["Academic.YearLevel"] = targetYear; // ✅ Dynamic year
            request.ExtraFields["Academic.Semester"] = targetSem; // ✅ Dynamic semester
            request.ExtraFields["Academic.AcademicYear"] = settings.AcademicYear ?? "";
            request.ExtraFields["Academic.Program"] = program;
            request.ExtraFields["Academic.EnrollmentType"] = isIrregular ? "Irregular" : "Regular";
            request.ExtraFields["AdvancedFrom"] = $"{currentYearLevel} {currentSemester}";
            request.ExtraFields["RequestDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            request.ExtraFields["EnrolledSubjects"] = string.Join(",", selectedSubjects);
            request.ExtraFields["SelectedSubjects"] = string.Join(",", selectedSubjects);
            request.ExtraFields["HasFailedSubjects"] = hasFailedSubjects.ToString();

            // ✅ Store retake metadata
            request.ExtraFields["RetakeSubjects"] = string.Join(",", selectedSubjects.Where(s => retakeCodes.Contains(s)));
            request.ExtraFields["RegularSubjects"] = string.Join(",", selectedSubjects.Where(s => !retakeCodes.Contains(s)));
            request.ExtraFields["TotalUnits"] = unitLoad.totalUnits.ToString();
            request.ExtraFields["RetakeUnits"] = unitLoad.retakeUnits.ToString();
            request.ExtraFields["RegularUnits"] = unitLoad.regularUnits.ToString();

            // Store eligibility calculation
            request.SecondSemesterEligibility = eligibilityList.ToDictionary(
                s => s.Code,
                s => s.IsEligible ? "Can enroll - All prerequisites passed" : s.EligibilityReason,
                StringComparer.OrdinalIgnoreCase
            );

            try
            {
                await _firebase.SubmitEnrollmentRequestAsync(request);

                try
                {
                    var link = Url.Action("RequestDetails2ndYear", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                    await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                    {
                        type = "2ndYearPending",
                        title = "New 2nd Year Enrollment",
                        message = $"{request.FullName} submitted 2nd Year enrollment as {enrollmentType}.",
                        severity = "info",
                        icon = "graduation-cap",
                        id = request.Id,
                        link,
                        email = request.Email,
                        program = request.Program,
                        status = request.Status,
                        academicYear = settings.AcademicYear,
                        submittedAt = request.SubmittedAt
                    });
                }
                catch { /* non-fatal */ }

                TempData["Success"] = $"✅ 2nd Year enrollment request submitted as {enrollmentType}! You selected {selectedSubjects.Length} subjects. Your request is now pending admin review.";
                return RedirectToAction(nameof(StudentDashboard));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to submit 2nd Year enrollment request: {ex.Message}";
                return RedirectToAction(nameof(SubjectSelection2ndYear));
            }
        }

        // ✅ TEMPORARY TEST METHOD (Remove after Phase 4)
        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> TestRetakeDetection()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
                return Json(new { error = "Not logged in" });

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
                return Json(new { error = "Student not found" });

            var latest = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (latest == null)
                return Json(new { error = "No enrollment found" });

            var enrolledYearLevel = latest?.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year") ?? "1st Year";
            var enrolledSemester = latest?.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester") ?? "1st Semester";
            var program = NormalizeProgramCode(latest?.Program ?? "BSIT");

            var is2ndYearEnrolled = enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                   enrolledSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            var previousRemarks = is2ndYearEnrolled
                ? await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(latest, _firebase)
                : latest?.ExtraFields?
                    .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                                  kvp => kvp.Value,
                                  StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string targetYearLevel = is2ndYearEnrolled ? "2nd Year" : "1st Year";
            string targetSemester = "2nd Semester";

            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(program);

            var retakeOpportunities = SubjectRetakeHelper.GetRetakeOpportunities(
                studentRemarks: previousRemarks,
                currentSemester: targetSemester,
                studentYearLevel: targetYearLevel,
                program: program,
                allSubjects: allProgramSubjects
            );

            return Json(new
            {
                success = true,
                studentUsername = username,
                enrolledYearLevel,
                enrolledSemester,
                targetYearLevel,
                targetSemester,
                program,
                totalSubjectsInProgram = allProgramSubjects.Count,
                previousRemarksCount = previousRemarks.Count,
                failedSubjects = previousRemarks.Where(r => r.Value.Equals("fail", StringComparison.OrdinalIgnoreCase)).Select(r => r.Key).ToList(),
                retakeOpportunitiesCount = retakeOpportunities.Count,
                eligibleRetakes = retakeOpportunities.Where(r => r.IsEligible).Select(r => new { r.Code, r.Title, r.Units }).ToList(),
                ineligibleRetakes = retakeOpportunities.Where(r => !r.IsEligible).Select(r => new { r.Code, r.Title, r.IneligibilityReason }).ToList()
            });
        }


        // ✅ TEMPORARY TEST METHOD (Remove after Phase 4)
        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> TestPrerequisiteBlocking()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
                return Json(new { error = "Not logged in" });

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
                return Json(new { error = "Student not found" });

            var latest = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (latest == null)
                return Json(new { error = "No enrollment found" });

            var program = NormalizeProgramCode(latest?.Program ?? "BSIT");

            var previousRemarks = latest?.ExtraFields?
                .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                              kvp => kvp.Value,
                              StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(program);

            // Test Case 1: Select CC102 (retake) + CC103 (depends on CC102)
            var testSelection1 = new List<string> { "CC102", "CC103" };
            var validation1 = SubjectRetakeHelper.ValidateSubjectSelection(
                testSelection1,
                allProgramSubjects,
                previousRemarks
            );

            // Test Case 2: Select only CC102 (retake)
            var testSelection2 = new List<string> { "CC102" };
            var validation2 = SubjectRetakeHelper.ValidateSubjectSelection(
                testSelection2,
                allProgramSubjects,
                previousRemarks
            );

            return Json(new
            {
                success = true,
                testCase1 = new
                {
                    selection = testSelection1,
                    isValid = validation1.isValid,
                    errors = validation1.errors,
                    expectedResult = "SHOULD FAIL (concurrent prerequisite conflict)"
                },
                testCase2 = new
                {
                    selection = testSelection2,
                    isValid = validation2.isValid,
                    errors = validation2.errors,
                    expectedResult = "SHOULD PASS (retake only)"
                }
            });
        }

        // ✅ TEMPORARY TEST METHOD (Remove after Phase 4)
        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> TestUnitLoadCalculation()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
                return Json(new { error = "Not logged in" });

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
                return Json(new { error = "Student not found" });

            var latest = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (latest == null)
                return Json(new { error = "No enrollment found" });

            var program = NormalizeProgramCode(latest?.Program ?? "BSIT");

            var previousRemarks = latest?.ExtraFields?
                .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                              kvp => kvp.Value,
                              StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(program);

            var retakeOpportunities = SubjectRetakeHelper.GetRetakeOpportunities(
                previousRemarks,
                "2nd Semester",
                "1st Year",
                program,
                allProgramSubjects
            );

            // Test: Select 2 regular subjects + 1 retake
            var testSelection = new List<string> { "CC102", "NET101", "GEE3" }; // CC102=retake, others=regular

            var unitLoad = SubjectRetakeHelper.CalculateUnitLoad(
                testSelection,
                allProgramSubjects,
                retakeOpportunities,
                maxUnits: 24
            );

            return Json(new
            {
                success = true,
                selectedSubjects = testSelection,
                regularUnits = unitLoad.regularUnits,
                retakeUnits = unitLoad.retakeUnits,
                totalUnits = unitLoad.totalUnits,
                exceedsLimit = unitLoad.exceedsLimit,
                maxUnitsAllowed = 24
            });
        }


        // Helper method (add if not exists)
        private List<SubjectRow> GetSubjectsForYearAndSemester(string program, string yearLevel, string semester)
        {
            var year = EnrollmentRules.ParseYearLevel(yearLevel);
            var is1stSem = semester.Contains("1st", StringComparison.OrdinalIgnoreCase);
            var isBSENT = string.Equals(program, "BSENT", StringComparison.OrdinalIgnoreCase);

            if (!isBSENT)
            {
                return year switch
                {
                    2 when is1stSem => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                    2 => E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                    _ => new List<SubjectRow>()
                };
            }
            else
            {
                return year switch
                {
                    2 when is1stSem => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear1stSem.Subjects.ToList(),
                    2 => E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList(),
                    _ => new List<SubjectRow>()
                };
            }
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit2ndYearEnrollment(
      string enrollmentType,
      string targetYearLevel,
      string targetSemester,
      FreshmenInfoModel info) // ✅ ADD: Accept form data
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student account not found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var currentEnrollment = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (currentEnrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // Validate: Must be 1st Year 2nd Semester enrolled
            var currentYearLevel = currentEnrollment.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "");
            var currentSemester = currentEnrollment.ExtraFields?.GetValueOrDefault("Academic.Semester", "");

            // ✅ FIXED: Allow BOTH scenarios:
            // 1. 1st Year 2nd Semester → 2nd Year 1st Semester
            // 2. 2nd Year 1st Semester → 2nd Year 2nd Semester
            bool isValid1stYearToSecondYear = currentYearLevel.Contains("1st Year", StringComparison.OrdinalIgnoreCase) &&
                                              currentSemester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase);

            bool isValid2ndYearFirstToSecond = currentYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                               currentSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            if (!isValid1stYearToSecondYear && !isValid2ndYearFirstToSecond)
            {
                TempData["Error"] = "2nd Year enrollment is only available for students who completed 1st Year 2nd Semester OR 2nd Year 1st Semester.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // Determine target year/semester based on current enrollment
            string targetYear, targetSem;
            if (isValid1stYearToSecondYear)
            {
                targetYear = "2nd Year";
                targetSem = "1st Semester";
            }
            else // isValid2ndYearFirstToSecond
            {
                targetYear = "2nd Year";
                targetSem = "2nd Semester";
            }

            var settings = await _firebase.GetEnrollmentSettingsAsync();

            // ✅ Validate enrollment window matches target semester
            if (!settings.IsOpen)
            {
                TempData["Error"] = "Enrollment window is closed.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ Check if admin opened the correct semester
            bool correctSemesterOpen = (targetSem.Contains("1st") && settings.Semester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase)) ||
                                       (targetSem.Contains("2nd") && settings.Semester.Contains("2nd Semester", StringComparison.OrdinalIgnoreCase));

            if (!correctSemesterOpen)
            {
                TempData["Error"] = $"Enrollment for {targetYear} {targetSem} is not currently open.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ NEW: Check for pending library penalties
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var penalties = await _firebase.GetStudentPenaltiesFromLibraryAsync(student.Email);
                    var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                    if (pendingPenalties.Any())
                    {
                        var totalPending = pendingPenalties.Sum(p => p.Amount);
                        TempData["Error"] = $"Cannot proceed with enrollment: You have {pendingPenalties.Count} pending library penalty(ies) totaling ₱{totalPending:N2}. Please settle your penalties at the library counter before enrolling.";
                        return RedirectToAction(nameof(StudentDashboard));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Submit2ndYearEnrollment] Error checking penalties: {ex.Message}");
                // Continue with enrollment if penalty check fails (non-blocking)
            }

            // ✅ ADD: Server-side validation of required fields
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.LastName)) errors.Add("Student Last Name required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.FirstName)) errors.Add("Student First Name required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.Sex)) errors.Add("Student Sex required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.ContactNumber)) errors.Add("Student Contact Number required.");
            if (string.IsNullOrWhiteSpace(info.StudentPersonal.EmailAddress)) errors.Add("Student Email required.");
            if (string.IsNullOrWhiteSpace(info.StudentAddress.HouseStreet)) errors.Add("Student Address required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.LastName)) errors.Add("Guardian Last Name required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.FirstName)) errors.Add("Guardian First Name required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.Sex)) errors.Add("Guardian Sex required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.ContactNumber)) errors.Add("Guardian Contact Number required.");
            if (string.IsNullOrWhiteSpace(info.GuardianPersonal.Relationship)) errors.Add("Guardian Relationship required.");

            if (errors.Count > 0)
            {
                TempData["Error"] = string.Join(" ", errors);
                return RedirectToAction(nameof(StudentDashboard));
            }

            // Get ALL 1st Year remarks (both semesters)
            var allRemarks = await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(currentEnrollment, _firebase);

            // ✅ FIX: Only check subjects the student actually took (has remarks for)
            var hasOngoingSubjects = allRemarks.Any(kv =>
                string.Equals(kv.Value, "ongoing", StringComparison.OrdinalIgnoreCase));

            if (hasOngoingSubjects)
            {
                TempData["Error"] = "Cannot proceed: some subjects are still marked Ongoing. Please wait for administrators to update subject remarks.";
                return RedirectToAction(nameof(StudentDashboard));
            }
            // Determine enrollment type based on failed subjects
            var hasFailedSubjects = allRemarks.Any(r =>
                string.Equals(r.Value, "fail", StringComparison.OrdinalIgnoreCase));

            var program = currentEnrollment.Program ?? "BSIT";

            // ✅ Compose full name and emergency contact
            string Compose(string l, string f, string m, string? e)
                => string.Join(" ", new[] { f, m, l, e }.Where(s => !string.IsNullOrWhiteSpace(s)));

            var fullName = Compose(info.StudentPersonal.LastName, info.StudentPersonal.FirstName,
                                   info.StudentPersonal.MiddleName, info.StudentPersonal.Extension);
            var emergency = Compose(info.GuardianPersonal.LastName, info.GuardianPersonal.FirstName,
                                    info.GuardianPersonal.MiddleName, info.GuardianPersonal.Extension);

            // ✅ Build ExtraFields from form data
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Student.LastName"] = info.StudentPersonal.LastName,
                ["Student.FirstName"] = info.StudentPersonal.FirstName,
                ["Student.MiddleName"] = info.StudentPersonal.MiddleName,
                ["Student.Extension"] = info.StudentPersonal.Extension ?? "",
                ["Student.Sex"] = info.StudentPersonal.Sex,
                ["Student.ContactNumber"] = info.StudentPersonal.ContactNumber,
                ["Student.EmailAddress"] = info.StudentPersonal.EmailAddress,
                ["StudentAddress.HouseStreet"] = info.StudentAddress.HouseStreet,
                ["StudentAddress.Barangay"] = info.StudentAddress.Barangay,
                ["StudentAddress.City"] = info.StudentAddress.City,
                ["StudentAddress.PostalCode"] = info.StudentAddress.PostalCode,
                ["Academic.Program"] = program,
                ["Academic.YearLevel"] = targetYear, 
                ["Academic.Semester"] = targetSem,   
                ["Academic.AcademicYear"] = settings.AcademicYear ?? "",
                ["Academic.EnrollmentType"] = hasFailedSubjects ? "Irregular" : "Regular",
                ["Guardian.LastName"] = info.GuardianPersonal.LastName,
                ["Guardian.FirstName"] = info.GuardianPersonal.FirstName,
                ["Guardian.MiddleName"] = info.GuardianPersonal.MiddleName,
                ["Guardian.Extension"] = info.GuardianPersonal.Extension ?? "",
                ["Guardian.Sex"] = info.GuardianPersonal.Sex,
                ["Guardian.ContactNumber"] = info.GuardianPersonal.ContactNumber,
                ["Guardian.Relationship"] = info.GuardianPersonal.Relationship,
                ["GuardianAddress.HouseStreet"] = info.GuardianAddress.HouseStreet,
                ["GuardianAddress.Barangay"] = info.GuardianAddress.Barangay,
                ["GuardianAddress.City"] = info.GuardianAddress.City,
                ["GuardianAddress.PostalCode"] = info.GuardianAddress.PostalCode,
                ["AdvancedFrom"] = $"{currentYearLevel} {currentSemester}",
                ["RequestDate"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
            };

            // Preserve old remarks for reference
            if (currentEnrollment.ExtraFields != null)
            {
                foreach (var kvp in currentEnrollment.ExtraFields)
                {
                    if (kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    {
                        extra[kvp.Key] = kvp.Value;
                    }
                }
            }

            // ✅ Determine enrollment type
            string actualEnrollmentType;
            if (targetSem.Contains("2nd"))
            {
                // Enrolling for 2nd Year 2nd Semester
                actualEnrollmentType = hasFailedSubjects ? "Sophomore-Irregular" : "Sophomore-Regular";
            }
            else
            {
                // Enrolling for 2nd Year 1st Semester
                actualEnrollmentType = hasFailedSubjects ? "2nd Year-Irregular" : "2nd Year-Regular";
            }

            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = student.Email,
                FullName = fullName,
                Program = program,
                Type = actualEnrollmentType, // ✅ Now correctly set for both semesters
                Status = targetSem.Contains("1st") ? "1st Sem Pending" : "2nd Sem Pending", // ✅ Dynamic status
                SubmittedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                EmergencyContactName = emergency,
                EmergencyContactPhone = info.GuardianPersonal.ContactNumber,
                DocumentFlags = currentEnrollment.DocumentFlags,
                ExtraFields = extra
            };

            // Calculate eligibility
            var secondYearSubjects = GetSubjectsForYearAndSemester(program, targetYear, targetSem); // ✅ Use dynamic target
            var prereqMap = SecondYearEnrollmentHelper.GetSecondYearPrerequisites(program);

            var eligibilityList = SecondYearEnrollmentHelper.Calculate2ndYearEligibility(
                secondYearSubjects,
                allRemarks,
                prereqMap);

            request.SecondSemesterEligibility = eligibilityList.ToDictionary(
                s => s.Code,
                s => s.IsEligible ? "Can enroll - All prerequisites passed" : s.EligibilityReason,
                StringComparer.OrdinalIgnoreCase
            );
            // SERVER-SIDE: prevent 2nd Year submission when any first-year remark is unresolved
         

            try
            {
                await _firebase.SubmitEnrollmentRequestAsync(request);

                // Send notification to admin
                try
                {
                    var link = Url.Action("RequestDetails2ndYear", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                    await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                    {
                        type = "2ndYearPending",
                        title = "New 2nd Year Enrollment",
                        message = $"{request.FullName} submitted 2nd Year enrollment ({actualEnrollmentType}).",
                        severity = "info",
                        icon = "graduation-cap",
                        id = request.Id,
                        link,
                        email = request.Email,
                        program = request.Program,
                        status = "1st Sem Pending",
                        academicYear = settings.AcademicYear,
                        submittedAt = request.SubmittedAt
                    });
                }
                catch { /* non-fatal */ }

                TempData["Success"] = $"✅ 2nd Year enrollment request submitted as {actualEnrollmentType}! Your request is now pending admin review.";
                return RedirectToAction(nameof(StudentDashboard));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to submit 2nd Year enrollment request: {ex.Message}";
                return RedirectToAction(nameof(StudentDashboard));
            }
        }

        // Helper to get program from request
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



        private Dictionary<string, List<string>> GetPrerequisiteInfo(string? program = null, string? yearLevel = null, string? semester = null)
        {
            var p = NormalizeProgramCode(program ?? "BSIT");
            var year = EnrollmentRules.ParseYearLevel(yearLevel ?? "1st Year");

            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                List<SubjectRow> targetSubjects;

                // Get next year 1st semester subjects
                if (year == 1)
                {
                    var nextYear = "2nd Year";
                    targetSubjects = GetSubjectsForYearAndSemester(p, nextYear, "1st Semester");
                }
                else
                {
                    targetSubjects = GetSubjectsForYearAndSemester(p, yearLevel ?? "1st Year", "2nd Semester");
                }

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

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> SubjectSelection()
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            // Load latest enrollment request to read subject remarks
            EnrollmentRequest? latest = null;
            if (!string.IsNullOrWhiteSpace(student.Email))
            {
                latest = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            }

            // ✅ CRITICAL: Detect if this is 2nd Year enrollment
            var enrolledYearLevel = latest?.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year") ?? "1st Year";
            var enrolledSemester = latest?.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester") ?? "1st Semester";

            var is2ndYearEnrolled = enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                   enrolledSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            // ✅ NEW: Get ALL previous remarks (for 2nd year students, this includes 1st year)
            var previousRemarks = is2ndYearEnrolled
                ? await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(latest, _firebase)
                : latest?.ExtraFields?
                    .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                                  kvp => kvp.Value,
                                  StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Determine program
            var programForSubjects = NormalizeProgramCode(latest?.Program ?? "BSIT");

            // ✅ Get correct target subjects based on current enrollment
            string targetYearLevel;
            string targetSemester;
            List<SubjectRow> targetSubjects;

            if (is2ndYearEnrolled)
            {
                // 2nd Year 1st Sem enrolled → target 2nd Year 2nd Sem
                targetYearLevel = "2nd Year";
                targetSemester = "2nd Semester";
                targetSubjects = string.Equals(programForSubjects, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList();
            }
            else
            {
                // 1st Year 1st Sem enrolled → target 1st Year 2nd Sem
                targetYearLevel = "1st Year";
                targetSemester = "2nd Semester";
                targetSubjects = string.Equals(programForSubjects, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
            }

            // Compute eligibility using ALL previous remarks
            var eligibility = CalculateEligibilityUsingRemarks(previousRemarks, targetSubjects);

            // ✅ NEW: Get all program subjects for retake lookup
            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(programForSubjects);

            // ✅ NEW: Find retake opportunities
            var retakeOpportunities = SubjectRetakeHelper.GetRetakeOpportunities(
                studentRemarks: previousRemarks,
                currentSemester: targetSemester,
                studentYearLevel: targetYearLevel,
                program: programForSubjects,
                allSubjects: allProgramSubjects
            );

            // ✅ NEW: Merge retake eligibility into main eligibility dictionary
            foreach (var retake in retakeOpportunities)
            {
                if (retake.IsEligible)
                {
                    eligibility[retake.Code] = "Can enroll (Retake)";
                }
                else
                {
                    eligibility[retake.Code] = $"Cannot retake: {retake.IneligibilityReason}";
                }
            }

            // Build view model
            var vm = new StudentDashboardViewModel
            {
                SecondSemesterSubjects = targetSubjects
                    .Select(s => new StudentSubjectScheduleRow
                    {
                        Code = s.Code,
                        Title = s.Title,
                        Units = s.Units,
                        PreRequisite = s.PreRequisite ?? ""
                    })
                    .ToList(),
                SecondSemesterEligibility = eligibility
            };

            // ✅ NEW: Pass retake opportunities to view
            ViewBag.RetakeOpportunities = retakeOpportunities;

            // Provide document flags
            ViewBag.DocumentFlags = latest?.DocumentFlags ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // ✅ Pass year/semester info to view
            ViewBag.TargetYearLevel = targetYearLevel;
            ViewBag.TargetSemester = targetSemester;
            ViewBag.EnrolledYearLevel = enrolledYearLevel;

            return View("~/Areas/Student/Views/Student/SubjectSelection.cshtml", vm);
        }

        // ✅ NEW: Helper method to calculate eligibility from remarks
        private Dictionary<string, string> CalculateEligibilityUsingRemarks(
            Dictionary<string, string> remarks,
            List<SubjectRow> targetSubjects)
        {
            var eligibility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subject in targetSubjects)
            {
                var prereqString = subject.PreRequisite ?? "";

                if (string.IsNullOrWhiteSpace(prereqString))
                {
                    eligibility[subject.Code] = "Can enroll - No prerequisites";
                    continue;
                }

                var prereqs = prereqString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                var failedPrereqs = prereqs
                    .Where(p => !remarks.TryGetValue(p, out var remark) ||
                               !remark.Equals("pass", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (failedPrereqs.Any())
                {
                    eligibility[subject.Code] = $"Cannot enroll - Prerequisites not met: {string.Join(", ", failedPrereqs)}";
                }
                else
                {
                    eligibility[subject.Code] = "Can enroll - All prerequisites passed";
                }
            }

            return eligibility;
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitIrregularSelection([FromForm] string[]? selectedSubjects)
        {
            var username = User?.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
            {
                await HttpContext.SignOutAsync("StudentCookie");
                return RedirectToAction(nameof(StudentLogin));
            }

            var student = await _firebase.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student record not found.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            var settings = await _firebase.GetEnrollmentSettingsAsync();
            if (!settings.IsOpen)
            {
                TempData["Error"] = "Enrollment window is closed.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ NEW: Check for pending library penalties
            try
            {
                if (!string.IsNullOrWhiteSpace(student.Email))
                {
                    var penalties = await _firebase.GetStudentPenaltiesFromLibraryAsync(student.Email);
                    var pendingPenalties = penalties.Where(p => !p.IsPaid).ToList();
                    if (pendingPenalties.Any())
                    {
                        var totalPending = pendingPenalties.Sum(p => p.Amount);
                        TempData["Error"] = $"Cannot proceed with enrollment: You have {pendingPenalties.Count} pending library penalty(ies) totaling ₱{totalPending:N2}. Please settle your penalties at the library counter before enrolling.";
                        return RedirectToAction(nameof(StudentDashboard));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SubmitIrregularSelection] Error checking penalties: {ex.Message}");
                // Continue with enrollment if penalty check fails (non-blocking)
            }

            var latest = await _firebase.GetLatestRequestByEmailAsync(student.Email);
            if (latest != null && (latest.Status?.EndsWith("Sem Pending", StringComparison.OrdinalIgnoreCase) == true))
            {
                TempData["Error"] = "A semester enrollment is already pending.";
                return RedirectToAction(nameof(StudentDashboard));
            }

            // ✅ Determine if this is 1st Year or 2nd Year enrollment
            var enrolledYearLevel = latest?.ExtraFields?.GetValueOrDefault("Academic.YearLevel", "1st Year") ?? "1st Year";
            var enrolledSemester = latest?.ExtraFields?.GetValueOrDefault("Academic.Semester", "1st Semester") ?? "1st Semester";

            var is2ndYearEnrollment = enrolledYearLevel.Contains("2nd Year", StringComparison.OrdinalIgnoreCase) &&
                                     enrolledSemester.Contains("1st Semester", StringComparison.OrdinalIgnoreCase);

            // ✅ Get ALL previous remarks
            var previousRemarks = is2ndYearEnrollment
                ? await SecondYearEnrollmentHelper.ExtractAllFirstYearRemarksAsync(latest, _firebase)
                : latest?.ExtraFields?
                    .Where(kvp => kvp.Key.StartsWith("SubjectRemarks.", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(kvp => kvp.Key.Substring("SubjectRemarks.".Length),
                                  kvp => kvp.Value,
                                  StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var programForEligibility = NormalizeProgramCode(latest?.Program ?? "BSIT");

            // ✅ Calculate eligibility using correct target subjects
            List<SubjectRow> targetSubjects;
            string targetYearLevel;
            string targetSemester;
            string semesterStatus;

            if (is2ndYearEnrollment)
            {
                targetSubjects = string.Equals(programForEligibility, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._2ndYear._2ndYear2ndSem.Subjects.ToList();
                targetYearLevel = "2nd Year";
                targetSemester = "2nd Semester";
                semesterStatus = "2nd Sem Pending";
            }
            else
            {
                targetSubjects = string.Equals(programForEligibility, "BSENT", StringComparison.OrdinalIgnoreCase)
                    ? E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList()
                    : E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects.ToList();
                targetYearLevel = "1st Year";
                targetSemester = "2nd Semester";
                semesterStatus = "2nd Sem Pending";
            }

            var eligibility = CalculateEligibilityUsingRemarks(previousRemarks, targetSubjects);

            // Validate selected subjects are eligible
            var allowedSelected = new List<string>();
            if (selectedSubjects != null && selectedSubjects.Length > 0)
            {
                foreach (var code in selectedSubjects)
                {
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    if (eligibility.TryGetValue(code, out var reason) && reason.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        allowedSelected.Add(code);
                }
            }

            if (!allowedSelected.Any())
            {
                TempData["Error"] = "You must select at least one eligible subject.";
                return RedirectToAction(nameof(SubjectSelection));
            }

            // ✅ NEW: Get all program subjects for validation
            // ✅ NEW: Get all program subjects for validation
            var allProgramSubjects = SubjectRetakeHelper.GetAllProgramSubjects(programForEligibility);

            // ✅ NEW: Get retake opportunities BEFORE validation
            var retakeOpportunities = SubjectRetakeHelper.GetRetakeOpportunities(
                previousRemarks,
                targetSemester,
                targetYearLevel,
                programForEligibility,
                allProgramSubjects
            );

            // ✅ NEW: Merge retake eligibility into main eligibility (BEFORE validation)
            foreach (var retake in retakeOpportunities)
            {
                if (retake.IsEligible)
                {
                    eligibility[retake.Code] = "Can enroll (Retake)";
                }
                else
                {
                    eligibility[retake.Code] = $"Cannot retake: {retake.IneligibilityReason}";
                }
            }

            // Validate selected subjects are eligible
            if (selectedSubjects != null && selectedSubjects.Length > 0)
            {
                foreach (var code in selectedSubjects)
                {
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    if (eligibility.TryGetValue(code, out var reason) && reason.StartsWith("Can enroll", StringComparison.OrdinalIgnoreCase))
                        allowedSelected.Add(code);
                }
            }

            if (!allowedSelected.Any())
            {
                TempData["Error"] = "You must select at least one eligible subject.";
                return RedirectToAction(nameof(SubjectSelection));
            }

            // ✅ NEW: Validate subject selection (blocks concurrent prerequisite conflicts)
            var validation = SubjectRetakeHelper.ValidateSubjectSelection(
                allowedSelected,
                allProgramSubjects,
                previousRemarks
            );

            if (!validation.isValid)
            {
                TempData["Error"] = string.Join("<br/>", validation.errors);
                return RedirectToAction(nameof(SubjectSelection));
            }

            // ✅ NEW: Calculate unit load (including retakes)
            var unitLoad = SubjectRetakeHelper.CalculateUnitLoad(
                allowedSelected,
                allProgramSubjects,
                retakeOpportunities,
                maxUnits: 24
            );

            // ✅ NEW: Warn if exceeds unit limit (soft limit - allow submission)
            if (unitLoad.exceedsLimit)
            {
                TempData["Warning"] = $"⚠️ Total unit load ({unitLoad.totalUnits} units) exceeds recommended maximum (24 units). Admin review may be required.";
            }

            // Build extra fields
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (latest?.ExtraFields != null)
            {
                foreach (var kv in latest.ExtraFields)
                    extra[kv.Key] = kv.Value ?? "";
            }

            extra["Academic.Program"] = extra.TryGetValue("Academic.Program", out var p) ? p : "BSIT";
            extra["Academic.YearLevel"] = targetYearLevel; // ✅ Set target year
            extra["Academic.Semester"] = targetSemester; // ✅ Set target semester
            extra["Academic.AcademicYear"] = settings.AcademicYear ?? "";
            extra["Academic.EnrollmentType"] = "Irregular";
            extra["EnrolledSubjects"] = string.Join(",", allowedSelected);

            // ✅ NEW: Store retake metadata
            var retakeCodes = retakeOpportunities
                .Where(r => r.IsEligible)
                .Select(r => r.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            extra["RetakeSubjects"] = string.Join(",", allowedSelected.Where(s => retakeCodes.Contains(s)));
            extra["RegularSubjects"] = string.Join(",", allowedSelected.Where(s => !retakeCodes.Contains(s)));
            extra["TotalUnits"] = unitLoad.totalUnits.ToString();
            extra["RetakeUnits"] = unitLoad.retakeUnits.ToString();
            extra["RegularUnits"] = unitLoad.regularUnits.ToString();

            Console.WriteLine($"[SubmitIrregularSelection] ✅ Stored retake metadata:");
            Console.WriteLine($"  - EnrolledSubjects: {extra["EnrolledSubjects"]}");
            Console.WriteLine($"  - RetakeSubjects: {extra["RetakeSubjects"]}");
            Console.WriteLine($"  - RegularSubjects: {extra["RegularSubjects"]}");
            Console.WriteLine($"  - TotalUnits: {extra["TotalUnits"]}");
            Console.WriteLine($"  - RetakeUnits: {extra["RetakeUnits"]}");
            Console.WriteLine($"  - RegularUnits: {extra["RegularUnits"]}");


            string fullName = student.Email; // Fallback
            if (extra.TryGetValue("Student.LastName", out var ln) && !string.IsNullOrWhiteSpace(ln))
            {
                var first = extra.GetValueOrDefault("Student.FirstName", "");
                var middle = extra.GetValueOrDefault("Student.MiddleName", "");
                var ext = extra.GetValueOrDefault("Student.Extension", "");
                fullName = string.Join(" ", new[] { first, middle, ln, ext }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = student.Email ?? "",
                FullName = fullName,
                Program = extra.TryGetValue("Academic.Program", out var prog) ? prog : "BSIT",
                Type = is2ndYearEnrollment ? "Sophomore-Irregular" : "Freshmen-Irregular", // ✅ Set correct type
                Status = semesterStatus,
                SubmittedAt = DateTime.UtcNow,
                EmergencyContactName = latest?.EmergencyContactName ?? "",
                EmergencyContactPhone = latest?.EmergencyContactPhone ?? "",
                ExtraFields = extra,
                SecondSemesterEligibility = eligibility
            };

            await _firebase.SubmitEnrollmentRequestAsync(request);

            try
            {
                var link = Url.Action("RequestDetails", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                {
                    type = "PendingSubmitted",
                    title = is2ndYearEnrollment ? "New 2nd Year Irregular Enrollment" : "New 2nd Semester Enrollment",
                    message = $"{request.FullName} submitted ({semesterStatus}) for {request.Program}.",
                    severity = "info",
                    icon = "hourglass-half",
                    id = request.Id,
                    link,
                    email = request.Email,
                    program = request.Program,
                    status = request.Status,
                    academicYear = settings.AcademicYear,
                    submittedAt = request.SubmittedAt
                });
            }
            catch { /* non-fatal */ }

            TempData["Success"] = $"✅ {targetYearLevel} {targetSemester} irregular enrollment submitted! Selected {allowedSelected.Count} subjects.";
            return RedirectToAction(nameof(StudentDashboard));
        }

        [HttpGet]
        public async Task<IActionResult> StudentLogin()
        {
            try
            {
                var settings = await _firebase.GetEnrollmentSettingsAsync();

                var ay = settings.AcademicYear ?? "";
                var semester = settings.Semester ?? "1st Semester";
                var isOpen = settings.IsOpen && (settings.ClosesAtUtc == null || DateTime.UtcNow < settings.ClosesAtUtc);

                string duration = "";
                if (isOpen)
                {
                    if (settings.OpenedAtUtc.HasValue && settings.ClosesAtUtc.HasValue)
                    {
                        var openedLocal = settings.OpenedAtUtc.Value.ToLocalTime().ToString("MMM d, yyyy");
                        var closesLocal = settings.ClosesAtUtc.Value.ToLocalTime().ToString("MMM d, yyyy");
                        duration = $"{openedLocal} to {closesLocal}";
                    }
                    else if (settings.OpenDurationSeconds.HasValue)
                    {
                        var ts = TimeSpan.FromSeconds(settings.OpenDurationSeconds.Value);
                        if (ts.TotalDays >= 1)
                            duration = $"{(int)ts.TotalDays} day(s)";
                        else if (ts.TotalHours >= 1)
                            duration = $"{(int)ts.TotalHours} hour(s)";
                        else
                            duration = $"{(int)ts.TotalMinutes} minute(s)";
                    }
                    else
                    {
                        duration = "scheduled window";
                    }
                }

                ViewBag.EnrollmentAcademicYear = ay;
                ViewBag.EnrollmentSemester = semester;
                ViewBag.EnrollmentOpen = isOpen;
                ViewBag.EnrollmentDuration = duration;
                ViewBag.EnrollmentOpenedAtUtc = settings.OpenedAtUtc?.ToString("o");
                ViewBag.EnrollmentClosesAtUtc = settings.ClosesAtUtc?.ToString("o");

                // NEW: fetch recent announcements (3)
                try
                {
                    var recent = await _firebase.GetRecentAnnouncementsAsync(3);
                    ViewBag.Announcements = recent;
                }
                catch
                {
                    ViewBag.Announcements = new List<Announcement>();
                }
            }
            catch
            {
                // Fail safe: do not prevent login page rendering when settings lookup fails
                ViewBag.EnrollmentAcademicYear = "";
                ViewBag.EnrollmentSemester = "1st Semester";
                ViewBag.EnrollmentOpen = false;
                ViewBag.EnrollmentDuration = "";
                ViewBag.Announcements = new List<Announcement>();
            }

            return View("~/Areas/Student/Views/Student/StudentLogin.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StudentLogin(string email, string password, string? returnUrl = null, bool rememberMe = false)
        {
            var emailNorm = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(emailNorm))
            {
                ViewBag.ErrorMessage = "Email is required.";
                return View("~/Areas/Student/Views/Student/StudentLogin.cshtml");
            }

            var student = await _firebase.GetStudentByEmailAsync(emailNorm);
            if (student == null || !BCrypt.Net.BCrypt.Verify(password, student.PasswordHash))
            {
                ViewBag.ErrorMessage = "Invalid email or password.";
                return View("~/Areas/Student/Views/Student/StudentLogin.cshtml");
            }

            if (student.FirstLogin)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, student.Username),
                    new Claim("MustChangePassword", "true")
                };
                var identity = new ClaimsIdentity(claims, "StudentCookie");
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync("StudentCookie", principal, new AuthenticationProperties { IsPersistent = rememberMe });
                return RedirectToAction(nameof(ChangePassword));
            }

            var normalClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, student.Username),
                new Claim(ClaimTypes.Role, "Student")
            };
            var normalIdentity = new ClaimsIdentity(normalClaims, "StudentCookie");
            var normalPrincipal = new ClaimsPrincipal(normalIdentity);
            await HttpContext.SignInAsync("StudentCookie", normalPrincipal, new AuthenticationProperties { IsPersistent = rememberMe });

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("DashboardPage", "Landing", new { area = "Student" });
        }

        [Authorize(AuthenticationSchemes = "StudentCookie")]
        [HttpGet]
        public IActionResult ChangePassword()
        {
            // Allow either MustChangePassword or already-logged-in Student
            var mustChange = User?.HasClaim(c => c.Type == "MustChangePassword" && c.Value == "true") == true;
            var hasStudentRole = User?.IsInRole("Student") == true;

            if (!mustChange && !hasStudentRole)
                return RedirectToAction(nameof(StudentLogin));

            // Signal the StudentLogin page to open the Change Password modal
            TempData["OpenChangePasswordModal"] = "1";

            // IMPORTANT: render the login view directly so the anti-forgery token is generated
            // for the current authenticated user (avoids 400 on post).
            return View("~/Areas/Student/Views/Student/StudentLogin.cshtml");
        }

        private static string? ValidatePasswordPolicy(string password, string? username)
        {
            if (string.IsNullOrWhiteSpace(password)) return "Password is required.";
            if (password.Length < 8 || password.Length > 64) return "Password must be 8 to 64 characters.";
            if (password.Any(char.IsWhiteSpace)) return "Password cannot contain spaces.";
            if (!password.Any(char.IsLower)) return "Password must include at least one lowercase letter.";
            if (!password.Any(char.IsUpper)) return "Password must include at least one uppercase letter.";
            if (!password.Any(char.IsDigit)) return "Password must include at least one number.";
            if (!Regex.IsMatch(password, @"[^A-Za-z0-9]")) return "Password must include at least one symbol.";
            if (!string.IsNullOrWhiteSpace(username) &&
                password.Contains(username, StringComparison.OrdinalIgnoreCase))
                return "Password must not contain your username.";
            return null;
        }

        [Authorize(AuthenticationSchemes = "StudentCookie")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
        {
            var mustChange = User?.HasClaim(c => c.Type == "MustChangePassword" && c.Value == "true") == true;
            var hasStudentRole = User?.IsInRole("Student") == true;

            if (!mustChange && !hasStudentRole)
            {
                TempData["CP_Error"] = "Your session expired. Please log in again.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(StudentLogin));
            }

            var username = User!.FindFirstValue(ClaimTypes.Name) ?? "";
            if (string.IsNullOrWhiteSpace(username))
            {
                TempData["CP_Error"] = "User not found.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(StudentLogin));
            }

            var policyError = ValidatePasswordPolicy(newPassword, username);
            if (policyError != null)
            {
                TempData["CP_Error"] = policyError;
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(StudentLogin));
            }

            if (newPassword != confirmPassword)
            {
                TempData["CP_Error"] = "Passwords do not match.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(StudentLogin));
            }

            try
            {
                var student = await _firebase.GetStudentByUsernameAsync(username);
                if (student == null)
                {
                    TempData["CP_Error"] = "User record not found.";
                    TempData["OpenChangePasswordModal"] = "1";
                    return RedirectToAction(nameof(StudentLogin));
                }

                student.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                student.FirstLogin = false;
                await _firebase.UpdateStudentAsync(student);

                await HttpContext.SignOutAsync("StudentCookie");
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, student.Username),
            new Claim(ClaimTypes.Role, "Student")
        };
                var identity = new ClaimsIdentity(claims, "StudentCookie");
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync("StudentCookie", principal);

                return RedirectToAction("DashboardPage", "Landing", new { area = "Student" });
            }
            catch
            {
                TempData["CP_Error"] = "An error occurred while changing your password. Please try again.";
                TempData["OpenChangePasswordModal"] = "1";
                return RedirectToAction(nameof(StudentLogin));
            }
        }

        // Helper method to calculate 2nd semester eligibility (now program-aware)
        private Dictionary<string, string> CalculateSecondSemesterEligibility(Dictionary<string, string> subjectRemarks, string program)
        {
            var eligibility = new Dictionary<string, string>();
            var prerequisites = new Dictionary<string, List<string>>
            {
                ["CC103"] = new List<string> { "CC101", "CC102" },  // Intermediate Programming
                ["NSTP2"] = new List<string> { "NSTP1" },           // NSTP2
                ["PT101"] = new List<string> { "CC101", "CC102" },  // Platform Technologies
                ["PE2"] = new List<string> { "PE1" }                // PE2
            };

            // pick program-aware second sem list
            IEnumerable<SubjectRow> secondSemSubjects;
            if (string.Equals(NormalizeProgramCode(program), "BSENT", StringComparison.OrdinalIgnoreCase))
            {
                secondSemSubjects = E_SysV0._01.Models.BSENTSubjectModels._1stYear._1stYear2ndSem.Subjects;
            }
            else
            {
                secondSemSubjects = E_SysV0._01.Models.BSITSubjectModels._1stYear._1stYear2ndSem.Subjects;
            }

            foreach (var subject in secondSemSubjects)
            {
                var canEnroll = true;
                var reason = "Can enroll";

                if (prerequisites.ContainsKey(subject.Code))
                {
                    var prereqs = prerequisites[subject.Code];
                    var failedPrereqs = prereqs.Where(prereq =>
                        !subjectRemarks.ContainsKey(prereq) ||
                        subjectRemarks[prereq] != "pass"
                    ).ToList();

                    if (failedPrereqs.Any())
                    {
                        canEnroll = false;
                        reason = $"Cannot enroll - Prerequisites not met: {string.Join(", ", failedPrereqs)}";
                    }
                    else
                    {
                        reason = "Can enroll - All prerequisites passed";
                    }
                }
                else
                {
                    reason = "Can enroll - No prerequisites";
                }

                eligibility[subject.Code] = reason;
            }

            return eligibility;
        }

        // Simple normalization helper for program strings (mirror AdminController behavior)
        private string NormalizeProgramCode(string? program)
        {
            var p = (program ?? "").Trim();
            if (string.IsNullOrWhiteSpace(p)) return "BSIT";

            p = p.ToUpperInvariant();

            if (p.Contains("BSENT", StringComparison.OrdinalIgnoreCase)) return "BSENT";
            if (p.Contains("BSIT", StringComparison.OrdinalIgnoreCase)) return "BSIT";

            if (ProgramCatalog.All.Any(x => string.Equals(x.Code, p, StringComparison.OrdinalIgnoreCase)))
                return ProgramCatalog.All.First(x => string.Equals(x.Code, p, StringComparison.OrdinalIgnoreCase)).Code;

            return "BSIT";
        }

        [Authorize(AuthenticationSchemes = "StudentCookie")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("StudentCookie");
            return RedirectToAction(nameof(StudentLogin));
        }


    }


}