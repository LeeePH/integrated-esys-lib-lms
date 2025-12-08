using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentMaterialController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentMaterialController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentMaterial/{classCode}/{contentId}")]
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
            if (contentItem == null || contentItem.Type != "material")
                return NotFound("Material not found.");

            if (contentItem.ClassId != classItem.Id)
                return NotFound("Material not found in this class.");

            var files = await _mongoDb.GetUploadsByContentIdAsync(contentId);
            var recents = await _mongoDb.GetRecentMaterialsByClassIdAsync(classItem.Id);
            var initials = GetInitials(classItem.InstructorName);

            var vm = new StudentMaterialViewModel
            {
                MaterialId = contentItem.Id ?? contentId,
                SubjectName = classItem.SubjectName ?? string.Empty,
                SubjectCode = classItem.SubjectCode ?? string.Empty,
                ClassCode = classItem.ClassCode ?? string.Empty,
                InstructorName = classItem.InstructorName ?? string.Empty,
                InstructorInitials = string.IsNullOrWhiteSpace(initials) ? "IN" : initials,

                MaterialTitle = contentItem.Title ?? string.Empty,
                Description = contentItem.Description ?? string.Empty,
                UploadedBy = contentItem.UploadedBy ?? string.Empty,
                UploadDate = contentItem.CreatedAt,
                RecentMaterials = recents ?? new List<string>(),
                Files = files.Select(f => new MaterialFile { FileName = f.FileName, FileUrl = f.FileUrl }).ToList()
            };

            return View("~/Views/StudentDb/StudentMaterial/Index.cshtml", vm);
        }

        [HttpGet("/StudentMaterial/GetComments")]
        public async Task<IActionResult> GetComments([FromQuery] string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return BadRequest(new { success = false, message = "Missing contentId" });
            var items = await _mongoDb.GetTaskCommentsAsync(contentId);
            var comments = items.Select(c => new
            {
                id = c.Id,
                authorName = c.AuthorName,
                role = c.Role,
                text = c.Text,
                createdAt = c.CreatedAt,
                replies = c.Replies.Select(r => new { authorName = r.AuthorName, role = r.Role, text = r.Text, createdAt = r.CreatedAt }).ToList()
            }).ToList();
            return Json(new { success = true, comments });
        }

        [HttpPost("/StudentMaterial/PostComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(string contentId, string classCode, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = user?.Role ?? "Student";
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return Json(new { success = false, message = "Class not found" });
            var item = await _mongoDb.AddTaskCommentAsync(contentId, classItem.Id, authorEmail, authorName, role, text ?? string.Empty);
            if (item == null) return Json(new { success = false, message = "Failed to add comment" });
            return Json(new { success = true, comment = new { id = item.Id, authorName = item.AuthorName, role = item.Role, text = item.Text, createdAt = item.CreatedAt, replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = r.Role, text = r.Text, createdAt = r.CreatedAt }).ToList() } });
        }

        [HttpPost("/StudentMaterial/PostReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(string commentId, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Student");
            var authorEmail = user?.Email ?? (email ?? "student@local");
            var role = user?.Role ?? "Student";
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = last.Role, text = last.Text, createdAt = last.CreatedAt } : null });
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
