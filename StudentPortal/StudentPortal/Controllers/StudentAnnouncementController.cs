using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentAnnouncementController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAnnouncementController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentAnnouncement/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Index", "StudentDb");

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "announcement")
                return NotFound("Announcement not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Announcement not found in this class.");

            var recent = (await _mongoDb.GetContentsByClassIdAsync(classItem.Id))
                .Where(c => c.Type == "announcement")
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => string.IsNullOrWhiteSpace(c.Title) ? "Announcement" : c.Title)
                .ToList();

            var initials = GetInitials(classItem.InstructorName);

            var vm = new StudentAnnouncementViewModel
            {
                SubjectName = classItem.SubjectName ?? string.Empty,
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                ClassCode = classItem.ClassCode ?? string.Empty,
                InstructorName = classItem.InstructorName ?? string.Empty,
                InstructorInitials = string.IsNullOrWhiteSpace(initials) ? "IN" : initials,

                Title = string.IsNullOrWhiteSpace(contentItem.Title) ? "Announcement" : contentItem.Title,
                Description = contentItem.Description ?? string.Empty,
                UploadedBy = contentItem.UploadedBy ?? string.Empty,
                CreatedAt = contentItem.CreatedAt,
                RecentAnnouncements = recent
            };

            ViewBag.CurrentPage = "subjects";
            return View("~/Views/StudentDb/StudentAnnouncement/Index.cshtml", vm);
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "IN";
        }
    }
}
