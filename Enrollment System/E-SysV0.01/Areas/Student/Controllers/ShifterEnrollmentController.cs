using E_SysV0._01.Models;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]
    public class ShifterEnrollmentController : Controller
    {
        private readonly MongoDBServices _db;
        private readonly EmailServices _email;

        public ShifterEnrollmentController(MongoDBServices db, EmailServices email)
        {
            _db = db;
            _email = email;
        }

        /// <summary>
        /// ✅ NEW: Direct access to shifter form (for logged-in students)
        /// </summary>
        [Authorize(Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> ShifterEnrollment()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });

            var student = await _db.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student account not found.";
                return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
            }

            // Check eligibility
            var eligibility = await CheckShifterEligibilityAsync(username);
            if (!eligibility.IsEligible)
            {
                TempData["Error"] = $"You are not eligible to shift. Reasons: {string.Join(", ", eligibility.Reasons)}";
                return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
            }

            // Check for existing pending request
            var existing = await _db.GetLatestShifterRequestByUsernameAsync(username);
            if (existing != null && existing.Status == "Pending")
            {
                TempData["Info"] = "You already have a pending shifter request.";
                return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
            }

            // Get enrollment record for academic info
            var enrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
            if (enrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
            }

            var ef = enrollment.ExtraFields ?? new Dictionary<string, string>();
            string GetField(string key) => ef.TryGetValue(key, out var v) ? v : string.Empty;

            // Determine available programs (opposite of current)
            var currentProgram = GetField("Academic.Program");
            var availablePrograms = new List<string>();
            if (currentProgram.Equals("BSIT", StringComparison.OrdinalIgnoreCase))
                availablePrograms.Add("BSENT");
            else if (currentProgram.Equals("BSENT", StringComparison.OrdinalIgnoreCase))
                availablePrograms.Add("BSIT");
            else
            {
                // Fallback: show both
                availablePrograms.Add("BSIT");
                availablePrograms.Add("BSENT");
            }

            var viewModel = new ShifterFormViewModel
            {
                LastName = GetField("Student.LastName"),
                FirstName = GetField("Student.FirstName"),
                MiddleName = GetField("Student.MiddleName"),
                Extension = GetField("Student.Extension"),
                Sex = GetField("Student.Sex"),
                ContactNumber = GetField("Student.ContactNumber"),
                EmailAddress = student.Email,
                CourseLastEnrolled = currentProgram,
                TotalUnitsEarned = eligibility.TotalUnitsEarned,
                TotalUnitsFailed = eligibility.TotalUnitsFailed,
                AvailablePrograms = availablePrograms,
                SubjectRemarks = eligibility.PassedSubjects.Concat(eligibility.FailedSubjects).ToList()
            };

            ViewBag.IsDirectAccess = true; // Flag to differentiate from token-based access
            ViewBag.StudentUsername = username;

            return View("~/Areas/Student/Views/Student/Shifter/ShifterEnrollment.cshtml", viewModel);
        }

        /// <summary>
        /// ✅ UPDATED: Submit shifter enrollment (for direct access - no token required)
        /// </summary>
        [Authorize(Roles = "Student")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitShifterEnrollment(
            string courseApplyingToShift,
            string reasonForShifting,
            IFormFile? endorsementLetter,
            IFormFile? libraryClearance)
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });

            var student = await _db.GetStudentByUsernameAsync(username);
            if (student == null)
            {
                TempData["Error"] = "Student account not found.";
                return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(courseApplyingToShift) || string.IsNullOrWhiteSpace(reasonForShifting))
            {
                TempData["Error"] = "Please fill in all required fields.";
                return RedirectToAction("ShifterEnrollment");
            }

            // Check if shifting to same program
            var enrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
            var currentProgram = enrollment?.ExtraFields?.TryGetValue("Academic.Program", out var prog) == true ? prog : "";

            if (courseApplyingToShift.Equals(currentProgram, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "You cannot shift to the same program you are currently enrolled in.";
                return RedirectToAction("ShifterEnrollment");
            }

            // Handle file uploads
            string? endorsementPath = null;
            string? clearancePath = null;

            if (endorsementLetter != null && endorsementLetter.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "shifter-documents");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{username}_endorsement_{Guid.NewGuid()}{Path.GetExtension(endorsementLetter.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await endorsementLetter.CopyToAsync(stream);
                }

                endorsementPath = $"/uploads/shifter-documents/{fileName}";
            }

            if (libraryClearance != null && libraryClearance.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "shifter-documents");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{username}_clearance_{Guid.NewGuid()}{Path.GetExtension(libraryClearance.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await libraryClearance.CopyToAsync(stream);
                }

                clearancePath = $"/uploads/shifter-documents/{fileName}";
            }

            // Get eligibility data for auto-fill
            var eligibility = await CheckShifterEligibilityAsync(username);

            // Get current semester/AY
            var settings = await _db.GetEnrollmentSettingsAsync();

            // Create new shifter request
            var request = new ShifterEnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                StudentUsername = username,
                Email = student.Email,
                LastName = enrollment?.ExtraFields?.TryGetValue("Student.LastName", out var ln) == true ? ln : "",
                FirstName = enrollment?.ExtraFields?.TryGetValue("Student.FirstName", out var fn) == true ? fn : "",
                MiddleName = enrollment?.ExtraFields?.TryGetValue("Student.MiddleName", out var mn) == true ? mn : "",
                Extension = enrollment?.ExtraFields?.TryGetValue("Student.Extension", out var ext) == true ? ext : null,
                Sex = enrollment?.ExtraFields?.TryGetValue("Student.Sex", out var sex) == true ? sex : "",
                ContactNumber = enrollment?.ExtraFields?.TryGetValue("Student.ContactNumber", out var cn) == true ? cn : "",
                CourseLastEnrolled = currentProgram,
                CourseApplyingToShift = courseApplyingToShift,
                TotalUnitsEarned = eligibility.TotalUnitsEarned,
                TotalUnitsFailed = eligibility.TotalUnitsFailed,
                ReasonForShifting = reasonForShifting,
                EndorsementLetterPath = endorsementPath,
                LibraryClearancePath = clearancePath,
                Status = "Pending",
                SubmittedDate = DateTime.UtcNow,
                CurrentSemester = settings.Semester,
                CurrentAcademicYear = settings.AcademicYear
            };

            await _db.SubmitShifterEnrollmentRequestAsync(request);

            TempData["Success"] = "Your shifter enrollment request has been submitted successfully. You will be notified via email once reviewed.";
            return RedirectToAction("StudentDashboard", "StudentAccount", new { area = "Student" });
        }

        // ===================================================
        // ✅ LEGACY: Token-based workflow (keep for email links)
        // ===================================================

        /// <summary>
        /// Display shifter enrollment form (accessed via token from email)
        /// </summary>
        [AllowAnonymous]
        [HttpGet("Student/ShifterEnrollment/WithToken")]
        public async Task<IActionResult> ShifterEnrollmentWithToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("Invalid token.");

            var tokenHash = ComputeSha256(token);
            var request = await _db.GetShifterEnrollmentRequestByTokenAsync(tokenHash);

            if (request == null)
            {
                TempData["Error"] = "Shifter enrollment request not found.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            if (request.AccessTokenExpires < DateTime.UtcNow)
            {
                TempData["Error"] = "This link has expired. Please request a new one from your dashboard.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            if (request.Status != "Draft" && request.Status != "Pending")
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            // Get student info
            var student = await _db.GetStudentByUsernameAsync(request.StudentUsername);
            if (student == null)
            {
                TempData["Error"] = "Student not found.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            // Get enrollment record for academic info
            var enrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
            if (enrollment == null)
            {
                TempData["Error"] = "No enrollment record found.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            var ef = enrollment.ExtraFields ?? new Dictionary<string, string>();
            string GetField(string key) => ef.TryGetValue(key, out var v) ? v : string.Empty;

            // Get subject remarks
            var eligibility = await CheckShifterEligibilityAsync(request.StudentUsername);

            // Determine opposite program
            var currentProgram = GetField("Academic.Program");
            var availablePrograms = new List<string>();
            if (currentProgram.Equals("BSIT", StringComparison.OrdinalIgnoreCase))
                availablePrograms.Add("BSENT");
            else if (currentProgram.Equals("BSENT", StringComparison.OrdinalIgnoreCase))
                availablePrograms.Add("BSIT");

            var viewModel = new ShifterFormViewModel
            {
                LastName = GetField("Student.LastName"),
                FirstName = GetField("Student.FirstName"),
                MiddleName = GetField("Student.MiddleName"),
                Extension = GetField("Student.Extension"),
                Sex = GetField("Student.Sex"),
                ContactNumber = GetField("Student.ContactNumber"),
                EmailAddress = student.Email,
                CourseLastEnrolled = currentProgram,
                TotalUnitsEarned = eligibility.TotalUnitsEarned,
                TotalUnitsFailed = eligibility.TotalUnitsFailed,
                AvailablePrograms = availablePrograms,
                SubjectRemarks = eligibility.PassedSubjects.Concat(eligibility.FailedSubjects).ToList()
            };

            ViewBag.Token = token;
            ViewBag.RequestId = request.Id;
            ViewBag.IsDirectAccess = false;

            return View("~/Areas/Student/Views/Student/Shifter/ShifterEnrollment.cshtml", viewModel);
        }

        /// <summary>
        /// Submit shifter enrollment form (token-based)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("Student/ShifterEnrollment/SubmitWithToken")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitShifterEnrollmentWithToken(
            string token,
            string courseApplyingToShift,
            string reasonForShifting,
            IFormFile? endorsementLetter,
            IFormFile? libraryClearance)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid token.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            var tokenHash = ComputeSha256(token);
            var request = await _db.GetShifterEnrollmentRequestByTokenAsync(tokenHash);

            if (request == null || request.AccessTokenExpires < DateTime.UtcNow)
            {
                TempData["Error"] = "Invalid or expired token.";
                return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(courseApplyingToShift) || string.IsNullOrWhiteSpace(reasonForShifting))
            {
                TempData["Error"] = "Please fill in all required fields.";
                return RedirectToAction("ShifterEnrollmentWithToken", new { token });
            }

            // Check if shifting to same program
            var student = await _db.GetStudentByUsernameAsync(request.StudentUsername);
            var enrollment = await _db.GetLatestRequestByEmailAsync(student!.Email);
            var currentProgram = enrollment?.ExtraFields?.TryGetValue("Academic.Program", out var prog) == true ? prog : "";

            if (courseApplyingToShift.Equals(currentProgram, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "You cannot shift to the same program you are currently enrolled in.";
                return RedirectToAction("ShifterEnrollmentWithToken", new { token });
            }

            // Handle file uploads
            string? endorsementPath = null;
            string? clearancePath = null;

            if (endorsementLetter != null && endorsementLetter.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "shifter-documents");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{request.StudentUsername}_endorsement_{Guid.NewGuid()}{Path.GetExtension(endorsementLetter.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await endorsementLetter.CopyToAsync(stream);
                }

                endorsementPath = $"/uploads/shifter-documents/{fileName}";
            }

            if (libraryClearance != null && libraryClearance.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "shifter-documents");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{request.StudentUsername}_clearance_{Guid.NewGuid()}{Path.GetExtension(libraryClearance.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await libraryClearance.CopyToAsync(stream);
                }

                clearancePath = $"/uploads/shifter-documents/{fileName}";
            }

            // Get eligibility data for auto-fill
            var eligibility = await CheckShifterEligibilityAsync(request.StudentUsername);

            // Update request with form data
            request.LastName = enrollment?.ExtraFields?.TryGetValue("Student.LastName", out var ln) == true ? ln : "";
            request.FirstName = enrollment?.ExtraFields?.TryGetValue("Student.FirstName", out var fn) == true ? fn : "";
            request.MiddleName = enrollment?.ExtraFields?.TryGetValue("Student.MiddleName", out var mn) == true ? mn : "";
            request.Extension = enrollment?.ExtraFields?.TryGetValue("Student.Extension", out var ext) == true ? ext : null;
            request.Sex = enrollment?.ExtraFields?.TryGetValue("Student.Sex", out var sex) == true ? sex : "";
            request.ContactNumber = enrollment?.ExtraFields?.TryGetValue("Student.ContactNumber", out var cn) == true ? cn : "";
            request.CourseLastEnrolled = currentProgram;
            request.CourseApplyingToShift = courseApplyingToShift;
            request.TotalUnitsEarned = eligibility.TotalUnitsEarned;
            request.TotalUnitsFailed = eligibility.TotalUnitsFailed;
            request.ReasonForShifting = reasonForShifting;
            request.EndorsementLetterPath = endorsementPath;
            request.LibraryClearancePath = clearancePath;
            request.Status = "Pending";
            request.SubmittedDate = DateTime.UtcNow;

            // Get current semester/AY
            var settings = await _db.GetEnrollmentSettingsAsync();
            request.CurrentSemester = settings.Semester;
            request.CurrentAcademicYear = settings.AcademicYear;

            await _db.UpdateShifterEnrollmentRequestAsync(request);

            TempData["Success"] = "Your shifter enrollment request has been submitted successfully.";
            return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
        }

        // ===================================================
        // HELPER METHODS
        // ===================================================

        /// <summary>
        /// Check if student is eligible to shift
        /// </summary>
        private async Task<ShifterEligibilityViewModel> CheckShifterEligibilityAsync(string studentUsername)
        {
            var result = new ShifterEligibilityViewModel
            {
                StudentUsername = studentUsername,
                IsEligible = true
            };

            var student = await _db.GetStudentByUsernameAsync(studentUsername);
            if (student == null)
            {
                result.IsEligible = false;
                result.Reasons.Add("Student account not found");
                return result;
            }

            result.Email = student.Email;

            // Must be enrolled (Regular or Irregular)
            var enrollment = await _db.GetLatestRequestByEmailAsync(student.Email);
            if (enrollment == null || !enrollment.Status.StartsWith("Enrolled", StringComparison.OrdinalIgnoreCase))
            {
                result.IsEligible = false;
                result.Reasons.Add("You must be currently enrolled");
                return result;
            }

            var ef = enrollment.ExtraFields ?? new Dictionary<string, string>();
            string GetField(string key) => ef.TryGetValue(key, out var v) ? v : string.Empty;

            result.FullName = $"{GetField("Student.FirstName")} {GetField("Student.LastName")}";
            result.CurrentProgram = GetField("Academic.Program");
            result.YearLevel = GetField("Academic.YearLevel");
            result.Semester = GetField("Academic.Semester");

            // Check enrollment settings - must be 2nd semester
            var settings = await _db.GetEnrollmentSettingsAsync();
            if (!settings.Semester.Equals("2nd Semester", StringComparison.OrdinalIgnoreCase))
            {
                result.IsEligible = false;
                result.Reasons.Add("Shifter enrollment is only available during 2nd Semester");
                return result;
            }

            // Check year level - only 1st and 2nd year
            var yearLevel = GetField("Academic.YearLevel");
            if (!yearLevel.StartsWith("1st", StringComparison.OrdinalIgnoreCase) &&
                !yearLevel.StartsWith("2nd", StringComparison.OrdinalIgnoreCase))
            {
                result.IsEligible = false;
                result.Reasons.Add("Only 1st and 2nd year students can shift");
                return result;
            }

            // Get subject remarks
            var remarks = await _db.GetStudentSubjectRemarksAsync(studentUsername);
            result.PassedSubjects = remarks.Where(r => r.Remark.Equals("Pass", StringComparison.OrdinalIgnoreCase)).ToList();
            result.FailedSubjects = remarks.Where(r => r.Remark.Equals("Fail", StringComparison.OrdinalIgnoreCase)).ToList();

            // Calculate total units
            result.TotalUnitsEarned = result.PassedSubjects.Sum(s => s.Units);
            result.TotalUnitsFailed = result.FailedSubjects.Sum(s => s.Units);

            if (result.IsEligible)
                result.Message = "You are eligible to shift programs.";
            else
                result.Message = "You are not currently eligible to shift.";

            return result;
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}