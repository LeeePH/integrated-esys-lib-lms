using E_SysV0._01.Hubs;
using E_SysV0._01.Models;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]
    public class TransfereeEnrollmentController : Controller
    {
        private readonly MongoDBServices _db;
        private readonly IHubContext<AdminNotificationsHub> _hub;
        private readonly EnrollmentCycleService _cycle;

        public TransfereeEnrollmentController(MongoDBServices db, IHubContext<AdminNotificationsHub> hub, EnrollmentCycleService cycle)
        {
            _db = db;
            _hub = hub;
            _cycle = cycle;
        }

        [HttpGet]
        public async Task<IActionResult> TransfereeEnrollment(string? program = null)
        {
            await _cycle.NormalizeAsync();
            var settings = await _db.GetEnrollmentSettingsAsync();
            var vm = new TransfereeEnrollmentViewModel();
            // Defaults
            vm.Academic.Semester = settings.Semester ?? "2nd Semester";
            vm.Academic.AcademicYear = settings.AcademicYear ?? "";

            ViewBag.IsSecondSemesterOpen = settings.IsOpen && (settings.ClosesAtUtc == null || DateTime.UtcNow < settings.ClosesAtUtc) && (settings.Semester?.StartsWith("2") ?? false);
            ViewBag.TransfereeBlocked = false;
            ViewBag.TransfereeBlockedMsg = "";
            ViewBag.ClosesAtUtc = settings.ClosesAtUtc?.ToString("o");

            // Prefer program from query string if provided and supported, otherwise fall back to the first program in the catalog.
            string defaultProgram;
            if (!string.IsNullOrWhiteSpace(program) && ProgramCatalog.IsSupported(program))
            {
                defaultProgram = ProgramCatalog.All.First(p => p.Code.Equals(program, StringComparison.OrdinalIgnoreCase)).Code;
            }
            else
            {
                defaultProgram = ProgramCatalog.All.First().Code;
            }

            ViewBag.ProgramOptions = ProgramCatalog.GetSelectList(defaultProgram);
            return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransfereeEnrollment(TransfereeEnrollmentViewModel model)
        {
            await _cycle.NormalizeAsync();
            var settings = await _db.GetEnrollmentSettingsAsync();

            // Ensure view data is available when returning the view on error
            ViewBag.IsSecondSemesterOpen = settings.IsOpen && (settings.ClosesAtUtc == null || DateTime.UtcNow < settings.ClosesAtUtc) && (settings.Semester?.StartsWith("2") ?? false);
            ViewBag.TransfereeBlocked = false;
            ViewBag.TransfereeBlockedMsg = "";
            ViewBag.ClosesAtUtc = settings.ClosesAtUtc?.ToString("o");

            // Provide sensible ProgramOptions for redisplay: prefer posted program if valid, otherwise catalog default
            var postedProgram = model.Academic?.Program;
            string selectedProgram;
            if (!string.IsNullOrWhiteSpace(postedProgram) && ProgramCatalog.IsSupported(postedProgram))
                selectedProgram = ProgramCatalog.All.First(p => p.Code.Equals(postedProgram, StringComparison.OrdinalIgnoreCase)).Code;
            else
                selectedProgram = ProgramCatalog.All.First().Code;
            ViewBag.ProgramOptions = ProgramCatalog.GetSelectList(selectedProgram);

            // Ensure model has academic defaults so the form keeps values when redisplayed
            // Fix: TransfereeEnrollmentViewModel.Academic is TransfereeAcademicInfo (not AcademicInfo)
            model.Academic ??= new TransfereeAcademicInfo();
            model.Academic.Semester = settings.Semester ?? model.Academic.Semester ?? "2nd Semester";
            model.Academic.AcademicYear = settings.AcademicYear ?? model.Academic.AcademicYear ?? "";

            // Basic window check
            bool isWindowOpen = settings.IsOpen && (settings.ClosesAtUtc == null || DateTime.UtcNow < settings.ClosesAtUtc)
                                && string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase);
            if (!isWindowOpen)
            {
                ModelState.AddModelError(string.Empty, "Transferee enrollment is not open at this time.");
                return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", model);
            }

            // Validate required fields
            var errs = new List<string>();
            if (string.IsNullOrWhiteSpace(model.StudentPersonal?.LastName)) errs.Add("Student Last Name required.");
            if (string.IsNullOrWhiteSpace(model.StudentPersonal?.FirstName)) errs.Add("Student First Name required.");
            if (string.IsNullOrWhiteSpace(model.StudentPersonal?.ContactNumber)) errs.Add("Student Contact Number required.");
            if (string.IsNullOrWhiteSpace(model.StudentPersonal?.EmailAddress)) errs.Add("Student Email required.");
            if (string.IsNullOrWhiteSpace(model.GuardianPersonal?.LastName)) errs.Add("Guardian Last Name required.");
            if (string.IsNullOrWhiteSpace(model.GuardianPersonal?.ContactNumber)) errs.Add("Guardian Contact Number required.");
            if (string.IsNullOrWhiteSpace(model.PreviousSchool?.LastSchoolAttended)) errs.Add("Previous School required.");
            if (errs.Count > 0)
            {
                // Preserve user input and show validation summary similar to Freshmen flow
                ModelState.AddModelError(string.Empty, string.Join(" ", errs));
                return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", model);
            }

            // Normalize email
            var email = (model.StudentPersonal.EmailAddress ?? "").Trim().ToLowerInvariant();

            // Prevent duplicate submissions (pending)
            var existing = await _db.GetLatestRequestByEmailAsync(email);
            if (existing != null && existing.Status != null && existing.Status.EndsWith("Sem Pending", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "An enrollment for this email is already pending.");
                return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", model);
            }
            // Check for existing application by full name (prevent duplicate applications by name)
            var fullName = $"{model.StudentPersonal.FirstName} {model.StudentPersonal.MiddleName} {model.StudentPersonal.LastName}"
                               .Replace("  ", " ").Trim();

            var existingByName = await _db.GetLatestRequestByFullNameAsync(fullName, "Transferee");
            if (existingByName != null)
            {
                // simple email masking for privacy (same approach used in Freshmen flow)
                string MaskEmail(string em)
                {
                    if (string.IsNullOrWhiteSpace(em)) return "";
                    var at = em.IndexOf('@');
                    if (at <= 1) return "***" + em;
                    var namePart = em.Substring(0, at);
                    var domain = em.Substring(at + 1);
                    var visible = Math.Min(2, namePart.Length);
                    return $"{namePart.Substring(0, visible)}***@{domain}";
                }

                var masked = MaskEmail(existingByName.Email);
                ModelState.AddModelError(string.Empty, $"An application for {fullName} already exists under {masked}.");
                return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", model);
            }
            // NEW: Prevent duplicate when already enrolled (by exact full name OR email)
            var fullNameForCheck = $"{model.StudentPersonal.FirstName} {model.StudentPersonal.MiddleName} {model.StudentPersonal.LastName}"
                                    .Replace("  ", " ").Trim();
            var alreadyEnrolled = await _db.ExistsEnrolledByNameOrEmailAsync(fullNameForCheck, email);
            if (alreadyEnrolled)
            {
                ModelState.AddModelError(string.Empty, "A student with that name or email is already enrolled. Duplicate enrollment is not allowed.");
                return View("~/Areas/Student/Views/Student/Transferee/TransfereeEnrollment.cshtml", model);
            }

            // Compose extra fields
            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Student.LastName"] = model.StudentPersonal.LastName ?? "",
                ["Student.FirstName"] = model.StudentPersonal.FirstName ?? "",
                ["Student.MiddleName"] = model.StudentPersonal.MiddleName ?? "",
                ["Student.Extension"] = model.StudentPersonal.Extension ?? "",
                ["Student.Sex"] = model.StudentPersonal.Sex ?? "",
                ["Student.ContactNumber"] = model.StudentPersonal.ContactNumber ?? "",
                ["Student.EmailAddress"] = email,

                ["StudentAddress.HouseStreet"] = model.StudentAddress.HouseStreet ?? "",
                ["StudentAddress.Barangay"] = model.StudentAddress.Barangay ?? "",
                ["StudentAddress.City"] = model.StudentAddress.City ?? "",
                ["StudentAddress.PostalCode"] = model.StudentAddress.PostalCode ?? "",

                ["Academic.Program"] = string.IsNullOrWhiteSpace(model.Academic?.Program) ? selectedProgram : model.Academic.Program,
                ["Academic.YearLevel"] = model.Academic?.YearLevel ?? "1st Year",
                ["Academic.Semester"] = settings.Semester ?? "2nd Semester",
                ["Academic.AcademicYear"] = settings.AcademicYear ?? "",

                ["ElementarySchool.SchoolName"] = model.ElementarySchool.SchoolName ?? "",
                ["ElementarySchool.SchoolType"] = model.ElementarySchool.SchoolType ?? "",
                ["ElementarySchool.YearGraduated"] = model.ElementarySchool.YearGraduated ?? "",
                ["ElementaryAddress.City"] = model.ElementaryAddress.City ?? "",
                ["ElementaryAddress.Barangay"] = model.ElementaryAddress.Barangay ?? "",
                ["ElementaryAddress.PostalCode"] = model.ElementaryAddress.PostalCode ?? "",

                ["HighSchool.SchoolName"] = model.HighSchool.SchoolName ?? "",
                ["HighSchool.SchoolType"] = model.HighSchool.SchoolType ?? "",
                ["HighSchool.YearGraduated"] = model.HighSchool.YearGraduated ?? "",
                ["HighSchoolAddress.City"] = model.HighSchoolAddress.City ?? "",
                ["HighSchoolAddress.Barangay"] = model.HighSchoolAddress.Barangay ?? "",
                ["HighSchoolAddress.PostalCode"] = model.HighSchoolAddress.PostalCode ?? "",

                ["SeniorHigh.SchoolName"] = model.SeniorHigh.SchoolName ?? "",
                ["SeniorHigh.SchoolType"] = model.SeniorHigh.SchoolType ?? "",
                ["SeniorHigh.YearGraduated"] = model.SeniorHigh.YearGraduated ?? "",
                ["SeniorHigh.Strand"] = model.SeniorHigh.Strand ?? "",
                ["SeniorHighAddress.City"] = model.SeniorHighAddress.City ?? "",
                ["SeniorHighAddress.Barangay"] = model.SeniorHighAddress.Barangay ?? "",
                ["SeniorHighAddress.PostalCode"] = model.SeniorHighAddress.PostalCode ?? "",

                ["Guardian.LastName"] = model.GuardianPersonal.LastName ?? "",
                ["Guardian.FirstName"] = model.GuardianPersonal.FirstName ?? "",
                ["Guardian.MiddleName"] = model.GuardianPersonal.MiddleName ?? "",
                ["Guardian.Extension"] = model.GuardianPersonal.Extension ?? "",
                ["Guardian.Sex"] = model.GuardianPersonal.Sex ?? "",
                ["Guardian.ContactNumber"] = model.GuardianPersonal.ContactNumber ?? "",
                ["Guardian.Relationship"] = model.GuardianPersonal.Relationship ?? "",

                ["GuardianAddress.HouseStreet"] = model.GuardianAddress.HouseStreet ?? "",
                ["GuardianAddress.Barangay"] = model.GuardianAddress.Barangay ?? "",
                ["GuardianAddress.City"] = model.GuardianAddress.City ?? "",
                ["GuardianAddress.PostalCode"] = model.GuardianAddress.PostalCode ?? "",

                ["PreviousSchool.LastSchoolAttended"] = model.PreviousSchool.LastSchoolAttended ?? "",
                ["PreviousSchool.CourseTaken"] = model.PreviousSchool.CourseTaken ?? "",
                ["PreviousSchool.LastAcademicYearAttended"] = model.PreviousSchool.LastAcademicYearAttended ?? ""
            };

            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = email,
                FullName = $"{model.StudentPersonal.FirstName} {model.StudentPersonal.MiddleName} {model.StudentPersonal.LastName}".Replace("  ", " ").Trim(),
                Program = extra.TryGetValue("Academic.Program", out var progVal) ? progVal : selectedProgram,
                Type = "Transferee",
                Status = "2nd Sem Pending",
                SubmittedAt = DateTime.UtcNow,
                EmergencyContactName = $"{model.GuardianPersonal.FirstName} {model.GuardianPersonal.MiddleName} {model.GuardianPersonal.LastName}".Replace("  ", " ").Trim(),
                EmergencyContactPhone = model.GuardianPersonal.ContactNumber ?? "",
                ExtraFields = extra
            };

            await _db.SubmitEnrollmentRequestAsync(request);

            // Notify admins
            try
            {
                var link = Url.Action("RequestDetails", "Admin", new { area = "Admin", id = request.Id }, Request.Scheme);
                await _hub.Clients.Group("Admins").SendAsync("AdminNotification", new
                {
                    type = "PendingSubmitted",
                    title = "New transferee pending",
                    message = $"{request.FullName} submitted (2nd Sem Pending) for {request.Program}.",
                    severity = "info",
                    icon = "hourglass-start",
                    id = request.Id,
                    link,
                    email = request.Email,
                    program = request.Program,
                    status = request.Status,
                    submittedAt = request.SubmittedAt
                });
            }
            catch { /* non-fatal */ }

            // Set Modal TempData keys (view expects ModalTitle/ModalBody/ModalVariant)
            TempData["ModalTitle"] = "Submission received";
            TempData["ModalBody"] = "Transferee enrollment submitted. Please hand TOR and documents to admin for verification.";
            TempData["ModalVariant"] = "success";
            // Keep legacy key for compatibility
            TempData["Success"] = "Transferee enrollment submitted. Please hand TOR and documents to admin for verification.";
            return RedirectToAction("TransfereeEnrollment");
        }
    }
}