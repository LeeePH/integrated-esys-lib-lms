using E_SysV0._01.Hubs;
using E_SysV0._01.Models;
using E_SysV0._01.Models.BSITSubjectModels._1stYear;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuestPDF;
namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]
    public class FreshmenEnrollmentController : Controller
    {
        private readonly MongoDBServices _firebase; private readonly IWebHostEnvironment _env;
        private readonly IHubContext<AdminNotificationsHub> _hub;
        private readonly EnrollmentCycleService _cycle; // NEW
        public FreshmenEnrollmentController(MongoDBServices firebase, IWebHostEnvironment env, IHubContext<AdminNotificationsHub> hub, EnrollmentCycleService cycle)
        {
            _firebase = firebase; _env = env; _hub = hub; _cycle = cycle; // NEW
        }
        [HttpGet]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> FreshmenEnrollment()
        { // Ensure cycle advances even without admin traffic await _cycle.NormalizeAsync();
            var settings = await _firebase.GetEnrollmentSettingsAsync();

            // Freshmen are not allowed during 2nd semester
            var freshmenBlocked = string.Equals(settings.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase);

            bool isWindowOpen = settings.IsOpen &&
                                (settings.ClosesAtUtc == null || DateTime.UtcNow < settings.ClosesAtUtc);

            if (freshmenBlocked)
            {
                isWindowOpen = false;
                ViewBag.FreshmenBlocked = true;
                ViewBag.FreshmenBlockedMsg = "Freshmen enrollment is closed during 2nd Semester. Please wait for the next semestral enrollment.";
            }

            ViewBag.EnrollmentClosed = !isWindowOpen;
            ViewBag.ClosesAtUtc = settings.ClosesAtUtc?.ToString("o");
            ViewBag.OpenedAtUtc = settings.OpenedAtUtc?.ToString("o");

            // Semester end for countdown
            var semEnd = string.Equals(settings.Semester, "1st Semester", StringComparison.OrdinalIgnoreCase)
                ? settings.Semester1EndsAtUtc
                : settings.Semester2EndsAtUtc;
            ViewBag.SemesterEndsAtUtc = semEnd?.ToString("o");

            // Program list (BSIT + defaults)
            var defaultProgram = "BSIT";
            ViewBag.ProgramOptions = ProgramCatalog.GetSelectList(defaultProgram);

            // Year/Sem defaults
            var yearLevel = SubjectCatalog.GetDefaultYearLevel(defaultProgram);
            var semester = SubjectCatalog.GetDefaultSemester(defaultProgram, settings.Semester);

            return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml",
                new FreshmenInfoModel
                {
                    Academic = new AcademicInfo
                    {
                        Program = defaultProgram,
                        YearLevel = yearLevel,
                        Semester = semester,
                        AcademicYear = settings.AcademicYear ?? ""
                    }
                });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FreshmenEnrollment(FreshmenInfoModel info)
        {
            // Ensure cycle advances even without admin traffic
            await _cycle.NormalizeAsync();

            void BindProgramOptions(string? selected)
            {
                ViewBag.ProgramOptions = ProgramCatalog.GetSelectList(selected ?? "BSIT");
            }

            async Task EnsureAcademicDefaultsAsync()
            {
                var settings = await _firebase.GetEnrollmentSettingsAsync();
                info.Academic ??= new AcademicInfo();

                if (!ProgramCatalog.IsSupported(info.Academic.Program))
                    info.Academic.Program = "BSIT";

                if (string.IsNullOrWhiteSpace(info.Academic.YearLevel))
                    info.Academic.YearLevel = SubjectCatalog.GetDefaultYearLevel(info.Academic.Program);

                if (string.IsNullOrWhiteSpace(info.Academic.Semester))
                    info.Academic.Semester = SubjectCatalog.GetDefaultSemester(info.Academic.Program, settings.Semester);

                if (string.IsNullOrWhiteSpace(info.Academic.AcademicYear))
                    info.Academic.AcademicYear = settings.AcademicYear ?? "";
            }

            var settingsNow = await _firebase.GetEnrollmentSettingsAsync();

            // Freshmen are not allowed during 2nd semester
            if (string.Equals(settingsNow.Semester, "2nd Semester", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Freshmen enrollment is closed during 2nd Semester. Please wait for the next cycle.");
                ViewBag.EnrollmentClosed = true;
                ViewBag.FreshmenBlocked = true;
                ViewBag.FreshmenBlockedMsg = "Freshmen enrollment is closed during 2nd Semester. Please wait for the next cycle.";
                ViewBag.ClosesAtUtc = settingsNow.ClosesAtUtc?.ToString("o");
                ViewBag.SemesterEndsAtUtc = (settingsNow.Semester2EndsAtUtc ?? settingsNow.Semester1EndsAtUtc)?.ToString("o");
                await EnsureAcademicDefaultsAsync();
                BindProgramOptions(info.Academic?.Program);
                return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml", info);
            }

            bool isWindowOpen = settingsNow.IsOpen &&
                                (settingsNow.ClosesAtUtc == null || DateTime.UtcNow < settingsNow.ClosesAtUtc);
            if (!isWindowOpen)
            {
                ModelState.AddModelError(string.Empty, "Enrollment is currently closed.");
                ViewBag.EnrollmentClosed = true;
                ViewBag.ClosesAtUtc = settingsNow.ClosesAtUtc?.ToString("o");
                ViewBag.SemesterEndsAtUtc = (settingsNow.Semester1EndsAtUtc ?? settingsNow.Semester2EndsAtUtc)?.ToString("o");
                await EnsureAcademicDefaultsAsync();
                BindProgramOptions(info.Academic?.Program);
                return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml", info);
            }

            static string ComposeName(string last, string first, string middle, string? ext)
            {
                var parts = new[] { first, middle, last, ext }.Where(p => !string.IsNullOrWhiteSpace(p));
                return System.Text.RegularExpressions.Regex.Replace(string.Join(" ", parts), @"\s+", " ").Trim();
            }

            string MaskEmail(string email)
            {
                if (string.IsNullOrWhiteSpace(email)) return "";
                var at = email.IndexOf('@');
                if (at <= 1) return "***" + email;
                var name = email[..at];
                var domain = email[(at + 1)..];
                var visible = Math.Min(2, name.Length);
                return $"{name[..visible]}***@{domain}";
            }

            var normalizedEmail = (info.StudentPersonal.EmailAddress ?? "").Trim().ToLowerInvariant();
            info.Academic ??= new AcademicInfo();
            info.Academic.Program = string.IsNullOrWhiteSpace(info.Academic.Program) ? "BSIT" : info.Academic.Program.Trim();
            info.Academic.YearLevel ??= "1st Year";
            info.Academic.Semester ??= "1st Semester";
            await EnsureAcademicDefaultsAsync(); // normalize values before using
            BindProgramOptions(info.Academic?.Program);
            var fullName = ComposeName(info.StudentPersonal.LastName, info.StudentPersonal.FirstName, info.StudentPersonal.MiddleName, info.StudentPersonal.Extension);
            var emergencyName = ComposeName(info.GuardianPersonal.LastName, info.GuardianPersonal.FirstName, info.GuardianPersonal.MiddleName, info.GuardianPersonal.Extension);
            var emergencyPhone = info.GuardianPersonal.ContactNumber ?? "";

            var existingStudent = await _firebase.GetStudentByEmailAsync(normalizedEmail);
            if (existingStudent != null)
            {
                ModelState.AddModelError(string.Empty, "This email is already associated with an accepted enrollment. Please use a different email or check your inbox for login details.");
                await EnsureAcademicDefaultsAsync();
                return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml", info);
            }

            var existingByEmail = await _firebase.GetLatestRequestByEmailAsync(normalizedEmail);
            if (existingByEmail != null)
            {
                ModelState.AddModelError(string.Empty, "An application has already been submitted using this email.");
                await EnsureAcademicDefaultsAsync();
                return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml", info);
            }

            var existingByName = await _firebase.GetLatestRequestByFullNameAsync(fullName, "Freshmen");
            if (existingByName != null)
            {
                var masked = MaskEmail(existingByName.Email);
                ModelState.AddModelError(string.Empty, $"An application for {fullName} already exists under {masked}.");
                await EnsureAcademicDefaultsAsync();
                return View("~/Areas/Student/Views/Student/Freshmen/FreshmenEnrollment.cshtml", info);
            }

            var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Student.LastName"] = info.StudentPersonal.LastName ?? "",
                ["Student.FirstName"] = info.StudentPersonal.FirstName ?? "",
                ["Student.MiddleName"] = info.StudentPersonal.MiddleName ?? "",
                ["Student.Extension"] = info.StudentPersonal.Extension ?? "",
                ["Student.Sex"] = info.StudentPersonal.Sex ?? "",
                ["Student.ContactNumber"] = info.StudentPersonal.ContactNumber ?? "",
                ["Student.EmailAddress"] = normalizedEmail,

                ["StudentAddress.HouseStreet"] = info.StudentAddress.HouseStreet ?? "",
                ["StudentAddress.Barangay"] = info.StudentAddress.Barangay ?? "",
                ["StudentAddress.City"] = info.StudentAddress.City ?? "",
                ["StudentAddress.PostalCode"] = info.StudentAddress.PostalCode ?? "",

                ["Academic.Program"] = info.Academic.Program,
                ["Academic.YearLevel"] = info.Academic.YearLevel,
                ["Academic.Semester"] = info.Academic.Semester,
                ["Academic.AcademicYear"] = info.Academic.AcademicYear ?? "",

                ["ElementarySchool.SchoolName"] = info.ElementarySchool.SchoolName ?? "",
                ["ElementarySchool.SchoolType"] = info.ElementarySchool.SchoolType ?? "",
                ["ElementarySchool.YearGraduated"] = info.ElementarySchool.YearGraduated ?? "",
                ["ElementaryAddress.City"] = info.ElementaryAddress.City ?? "",
                ["ElementaryAddress.Barangay"] = info.ElementaryAddress.Barangay ?? "",
                ["ElementaryAddress.PostalCode"] = info.ElementaryAddress.PostalCode ?? "",

                ["HighSchool.SchoolName"] = info.HighSchool.SchoolName ?? "",
                ["HighSchool.SchoolType"] = info.HighSchool.SchoolType ?? "",
                ["HighSchool.YearGraduated"] = info.HighSchool.YearGraduated ?? "",
                ["HighSchoolAddress.City"] = info.HighSchoolAddress.City ?? "",
                ["HighSchoolAddress.Barangay"] = info.HighSchoolAddress.Barangay ?? "",
                ["HighSchoolAddress.PostalCode"] = info.HighSchoolAddress.PostalCode ?? "",

                ["SeniorHigh.SchoolName"] = info.SeniorHigh.SchoolName ?? "",
                ["SeniorHigh.SchoolType"] = info.SeniorHigh.SchoolType ?? "",
                ["SeniorHigh.YearGraduated"] = info.SeniorHigh.YearGraduated ?? "",
                ["SeniorHigh.Strand"] = info.SeniorHigh.Strand ?? "",
                ["SeniorHighAddress.City"] = info.SeniorHighAddress.City ?? "",
                ["SeniorHighAddress.Barangay"] = info.SeniorHighAddress.Barangay ?? "",
                ["SeniorHighAddress.PostalCode"] = info.SeniorHighAddress.PostalCode ?? "",

                ["Guardian.LastName"] = info.GuardianPersonal.LastName ?? "",
                ["Guardian.FirstName"] = info.GuardianPersonal.FirstName ?? "",
                ["Guardian.MiddleName"] = info.GuardianPersonal.MiddleName ?? "",
                ["Guardian.Extension"] = info.GuardianPersonal.Extension ?? "",
                ["Guardian.ContactNumber"] = info.GuardianPersonal.ContactNumber ?? "",
                ["Guardian.Relationship"] = info.GuardianPersonal.Relationship ?? "",

                ["GuardianAddress.HouseStreet"] = info.GuardianAddress.HouseStreet ?? "",
                ["GuardianAddress.Barangay"] = info.GuardianAddress.Barangay ?? "",
                ["GuardianAddress.City"] = info.GuardianAddress.City ?? "",
                ["GuardianAddress.PostalCode"] = info.GuardianAddress.PostalCode ?? ""
            };

            var request = new EnrollmentRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                FullName = fullName,
                Email = normalizedEmail,
                EmergencyContactName = emergencyName,
                EmergencyContactPhone = emergencyPhone,
                Program = info.Academic.Program,
                Type = "Freshmen",
                Status = "1st Sem Pending", // CHANGED from "Account Pending"
                SubmittedAt = DateTime.UtcNow,
                ExtraFields = extra
            };

            await _firebase.SubmitEnrollmentRequestAsync(request);

            await _hub.Clients.Group("Admins").SendAsync("NewEnrollmentRequest", new
            {
                id = request.Id,
                type = request.Type,
                fullName = request.FullName,
                email = request.Email,
                submittedAt = request.SubmittedAt.ToString("o"),
                status = request.Status,
                program = request.Program
            });

            TempData["ModalTitle"] = "Submission received";
            TempData["ModalBody"] = "Your basic information has been submitted. Kindly hand your physical documents to the admin, and then wait to receive your printed Registration Form.";
            TempData["ModalVariant"] = "success";

            return RedirectToAction(nameof(FreshmenEnrollment));
        }
    }
}