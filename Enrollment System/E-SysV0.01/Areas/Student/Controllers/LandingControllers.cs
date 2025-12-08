using E_SysV0._01.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]
    public class LandingController : Controller
    {
        private readonly MongoDBServices _db;

        public LandingController(MongoDBServices db)
        {
            _db = db;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult LandingPage()
        {
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole("Student"))
                return RedirectToAction(nameof(DashboardPage));
            return RedirectToAction("StudentLogin", "StudentAccount", new { area = "Student" });
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> DashboardPage()
        {
            await SetStudentNameAsync();
            return View("~/Areas/Student/Views/Student/SupersystemDashboard.cshtml");
        }

        [Authorize(AuthenticationSchemes = "StudentCookie", Roles = "Student")]
        [HttpGet]
        public async Task<IActionResult> EnrollmentLandingPage()
        {
            await SetStudentNameAsync();
            return View("~/Areas/Student/Views/Student/EnrollmentLandingPage.cshtml");
        }


        [AllowAnonymous]
        [HttpGet]
        public IActionResult FreshmenForm()
           => RedirectToAction("FreshmenEnrollment", "FreshmenEnrollment", new { area = "Student" });

        [AllowAnonymous]
        [HttpGet]
        public IActionResult ShifterForm()
           => RedirectToAction("ShifterEnrollment", "ShifterEnrollment", new { area = "Student" });

        [AllowAnonymous]
        [HttpGet]
        public IActionResult TransfereeForm()
            => RedirectToAction("TransfereeEnrollment", "TransfereeEnrollment", new { area = "Student" });

        private async Task SetStudentNameAsync()
        {
            string greet = "Student";
            var username = User?.FindFirstValue(ClaimTypes.Name);

            if (!string.IsNullOrWhiteSpace(username))
            {
                var student = await _db.GetStudentByUsernameAsync(username);
                if (student != null && !string.IsNullOrWhiteSpace(student.Email))
                {
                    var req = await _db.GetLatestRequestByEmailAsync(student.Email);
                    var ef = req?.ExtraFields ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    string EF(string key) => ef.TryGetValue(key, out var v) ? (v ?? "").Trim() : "";

                    var ln = EF("Student.LastName");
                    var fn = EF("Student.FirstName");
                    var mn = EF("Student.MiddleName");

                    if (!string.IsNullOrWhiteSpace(ln) || !string.IsNullOrWhiteSpace(fn) || !string.IsNullOrWhiteSpace(mn))
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(ln)) parts.Add(ln);
                        if (!string.IsNullOrWhiteSpace(fn)) parts.Add(fn);
                        if (!string.IsNullOrWhiteSpace(mn)) parts.Add(mn);

                        if (parts.Count > 0)
                        {
                            greet = parts.Count >= 2
                                ? $"{parts[0]}, {string.Join(", ", parts.Skip(1))}"
                                : parts[0];
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(req?.FullName))
                    {
                        greet = req.FullName;
                    }
                    else
                    {
                        greet = student.Username;
                    }
                }
                else if (student != null)
                {
                    greet = student.Username;
                }
            }

            ViewBag.GreetName = greet;
            ViewBag.DisplayName = greet;
        }
    }
}