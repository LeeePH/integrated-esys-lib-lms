using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAssessmentController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentAssessment/{classCode}/{contentId}")]
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
            if (contentItem == null || contentItem.Type != "assessment")
                return NotFound("Assessment not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Assessment not found in this class.");

            var user = await _mongoDb.GetUserByEmailAsync(email);

            var model = new StudentAssessmentViewModel
            {
                AssessmentTitle = contentItem.Title ?? "Assessment",
                Description = contentItem.Description ?? string.Empty,
                PostedDate = contentItem.CreatedAt.ToString("MMM d, yyyy"),
                Deadline = contentItem.Deadline?.ToString("MMM d, yyyy") ?? string.Empty,
                StudentName = user?.FullName ?? "Student",
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                Attachments = new System.Collections.Generic.List<StudentPortal.Models.StudentDb.TaskAttachment>(),
                ClassCode = classItem.ClassCode ?? classCode,
                ContentId = contentItem.Id ?? contentId
            };

            if (user != null)
            {
                var result = await _mongoDb.GetAssessmentResultAsync(classItem.Id, contentItem.Id, user.Id ?? string.Empty);
                if (result != null)
                {
                    model.IsAnswered = result.SubmittedAt.HasValue;
                    model.Score = result.Score;
                    model.MaxScore = result.MaxScore;
                }
            }

            return View("~/Views/StudentDb/StudentAssessment/Index.cshtml", model);
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsDone(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Index", "StudentDb");
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null) return RedirectToAction("Index", new { classCode, contentId });
            await _mongoDb.UpsertAssessmentSubmittedAsync(classItem.Id, classItem.ClassCode, contentItem.Id, user.Id ?? string.Empty, user.Email ?? string.Empty);
            TempData["ToastMessage"] = "? Assessment marked as done!";
            return RedirectToAction("Index", new { classCode, contentId });
        }
    }
}
