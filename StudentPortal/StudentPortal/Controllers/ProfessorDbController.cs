using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.ProfessorDb;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.ProfessorDb
{
    [Route("professordb/[controller]")]
    public class ProfessorDbController : Controller
    {
        private readonly MongoDbService _mongo;

        public ProfessorDbController(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        // GET: /ProfessorDb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            // Get professor info from session
            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            // Compute initials from name
            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            // For professors, show only their own classes (by owner email)
            var classes = await _mongo.GetClassesByOwnerEmailAsync(professorEmail);

            var vm = new ProfessorDashboardViewModel
            {
                ProfessorName = professorName,
                ProfessorInitials = GetInitials(professorName),
                Classes = classes.ToList()
            };

            ViewBag.Role = "Professor";
            return View("~/Views/ProfessorDb/ProfessorDb/Index.cshtml", vm);
        }

        // POST: /ProfessorDb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string year, string course, string semester)
        {
            // Basic validation (same pattern as AdminDbController)
            if (string.IsNullOrWhiteSpace(subjectName) ||
                string.IsNullOrWhiteSpace(subjectCode) ||
                string.IsNullOrWhiteSpace(section) ||
                string.IsNullOrWhiteSpace(year) ||
                string.IsNullOrWhiteSpace(course) ||
                string.IsNullOrWhiteSpace(semester))
            {
                TempData["ToastMessage"] = "⚠️ Please fill all required fields.";
                return RedirectToAction("Index");
            }

            var professorName = HttpContext.Session.GetString("UserName") ?? "Professor";
            var professorEmail = HttpContext.Session.GetString("UserEmail") ?? "";

            string GetInitials(string fullName)
            {
                if (string.IsNullOrWhiteSpace(fullName)) return "PR";
                var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(parts.Select(p => p[0])).ToUpper();
            }

            var creatorInitials = GetInitials(professorName);

            var newClass = new StudentPortal.Models.AdminDb.ClassItem
            {
                SubjectName = subjectName,
                SubjectCode = subjectCode,
                Section = section,
                Course = course,
                Year = year,
                Semester = semester,
                CreatorName = professorName,
                CreatorInitials = creatorInitials,
                CreatorRole = "Professor",
                OwnerEmail = professorEmail,
                ClassCode = _mongo.GenerateClassCode(),
                BackgroundImageUrl = ""
            };

            await _mongo.CreateClassAsync(newClass);

            TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" (Section: {newClass.SectionLabel}, Code: {newClass.ClassCode}) created successfully!";
            return RedirectToAction("Index");
        }
    }
}


