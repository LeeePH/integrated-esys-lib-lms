using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminDb;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.AdminDb
{
    [Route("admindb/[controller]")]
    public class AdminDbController : Controller
    {
        private readonly MongoDbService _mongo;

        public AdminDbController(MongoDbService mongo)
        {
            _mongo = mongo;
        }

        // GET: /admindb/admindb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var classes = await _mongo.GetAllClassesAsync();
            var vm = new AdminDashboardViewModel
            {
                AdminName = "Admin",
                AdminInitials = "AD",
                Classes = classes.ToList()
            };

            return View("~/Views/AdminDb/AdminDb/Index.cshtml", vm);
        }

        // POST: /admindb/admindb/CreateClass
        [HttpPost("CreateClass")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(string subjectName, string subjectCode, string section, string year, string course, string semester)
        {
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

            // Check if class with same section exists
            if (await _mongo.ClassExistsAsync(subjectName, section, year, course, semester))
            {
                TempData["ToastMessage"] = $"⚠️ Class \"{subjectName}\" ({course} {year} {section}) already exists for {semester} semester.";
                return RedirectToAction("Index");
            }

            var creatorName = User?.Identity?.Name ?? "Admin";
            var creatorInitials = new string(creatorName
                .Where(char.IsLetter)
                .Take(2)
                .ToArray())
                .ToUpper();

            var newClass = new ClassItem
            {
                SubjectName = subjectName,
                SubjectCode = subjectCode,
                Section = section,
                Course = course,
                Year = year,
                Semester = semester,
                CreatorName = creatorName,
                CreatorInitials = creatorInitials,
                CreatorRole = "Creator",
                ClassCode = _mongo.GenerateClassCode(),
                BackgroundImageUrl = ""
            };

            await _mongo.CreateClassAsync(newClass);

            TempData["ToastMessage"] = $"✅ Class \"{subjectName}\" (Section: {newClass.SectionLabel}, Code: {newClass.ClassCode}) created successfully!";
            return RedirectToAction("Index");
        }
    }
}
