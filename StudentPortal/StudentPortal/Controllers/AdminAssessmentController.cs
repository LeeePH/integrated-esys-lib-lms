using Microsoft.AspNetCore.Mvc;
using SIA_IPT.Models.AdminAssessment;
using StudentPortal.Services;
using StudentPortal.Models.AdminDb;
using System.Linq;
using System.Text.Json;

namespace SIA_IPT.Controllers
{
    public class AdminAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

		public AdminAssessmentController(MongoDbService mongoDb)
		{
			_mongoDb = mongoDb;
		}
        [HttpGet("/AdminAssessment/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var content = await _mongoDb.GetContentByIdAsync(contentId);
            if (classItem == null || content == null || content.ClassId != classItem.Id || content.Type != "assessment")
            {
                return NotFound();
            }

            var recent = await _mongoDb.GetRecentMaterialsByClassIdAsync(classItem.Id);
            var vm = new AdminAssessmentViewModel
            {
                AssessmentId = content.Id ?? string.Empty,
                SubjectName = classItem.SubjectName,
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                InstructorName = classItem.InstructorName,
                InstructorInitials = string.IsNullOrEmpty(classItem.InstructorName) ? "IN" : new string(classItem.InstructorName.Split(' ').Select(w => w[0]).ToArray()),
                RecentMaterials = recent,
                AssessmentTitle = content.Title,
                AssessmentDescription = content.Description,
                Attachments = content.Attachments ?? new List<string>(),
                PostedDate = content.CreatedAt.ToLocalTime().ToString("MMM d, yyyy"),
                Deadline = content.Deadline.HasValue ? content.Deadline.Value.ToLocalTime().ToString("MMM d, yyyy") : "N/A",
                Submissions = new List<StudentSubmission>()
            };

            var logs = await _mongoDb.GetAntiCheatLogsAsync(classItem.Id, content.Id);
            int copy = 0, paste = 0, inspect = 0, tabSwitch = 0, openPrograms = 0, screenShare = 0;
            foreach (var l in logs)
            {
                var count = l.EventCount > 0 ? l.EventCount : 1;
                switch ((l.EventType ?? string.Empty).ToLower())
                {
                    case "copy_paste":
                        try
                        {
                            using var doc = JsonDocument.Parse(l.Details ?? "{}");
                            var root = doc.RootElement;
                            var action = root.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
                            if (string.Equals(action, "paste", System.StringComparison.OrdinalIgnoreCase)) paste += count; else copy += count;
                        }
                        catch { copy += count; }
                        break;
                    case "inspect":
                        inspect += count; break;
                    case "tab_switch":
                        tabSwitch += count; break;
                    case "open_programs":
                        openPrograms += count; break;
                    case "screen_share":
                        screenShare += count; break;
                }
            }

            vm.LogCopy = copy;
            vm.LogPaste = paste;
            vm.LogInspect = inspect;
            vm.LogTabSwitch = tabSwitch;
            vm.LogOpenPrograms = openPrograms;
            vm.LogScreenShare = screenShare;

            return View("~/Views/AdminDb/AdminAssessment/Index.cshtml", vm);
        }

        [HttpPost("/AdminAssessment/{classCode}/{contentId}/set-score/{studentId}")]
        public async Task<IActionResult> SetScore(string classCode, string contentId, string studentId, [FromForm] double score, [FromForm] double maxScore)
        {
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (classItem == null || contentItem == null) return NotFound();
            await _mongoDb.UpdateAssessmentScoreAsync(classItem.Id, contentItem.Id, studentId, score, maxScore);
            return Ok(new { status = "score_set" });
        }

        [HttpPost("/AdminAssessment/UpdateAssessment")]
        public async Task<IActionResult> UpdateAssessment([FromBody] UpdateContentRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.AssessmentId)) return BadRequest(new { success = false, message = "Invalid request" });
            var content = await _mongoDb.GetContentByIdAsync(req.AssessmentId);
            if (content == null || content.Type != "assessment") return Json(new { success = false, message = "Assessment not found" });
            content.Title = req.Title ?? content.Title;
            content.Description = req.Description ?? content.Description;
            if (!string.IsNullOrEmpty(req.Deadline))
            {
                if (DateTime.TryParse(req.Deadline, out var dl)) content.Deadline = dl;
            }
            content.UpdatedAt = DateTime.UtcNow;
            await _mongoDb.UpdateContentAsync(content);
            return Json(new { success = true });
        }

        [HttpPost("/AdminAssessment/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceAssessmentAttachmentRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.AssessmentId) || string.IsNullOrEmpty(request.FileName))
                return BadRequest(new { success = false, message = "Invalid request" });

            var content = await _mongoDb.GetContentByIdAsync(request.AssessmentId);
            if (content == null || content.Type != "assessment")
                return Json(new { success = false, message = "Assessment not found" });

            content.Attachments = new List<string> { request.FileName };
            await _mongoDb.UpdateContentAsync(content);

            var uploads = await _mongoDb.GetUploadsByClassIdAsync(content.ClassId);
            var recentUpload = uploads
                .Where(u => u.UploadedBy == (User?.Identity?.Name ?? "Admin") && u.FileName == request.FileName)
                .OrderByDescending(u => u.UploadedAt)
                .FirstOrDefault();
            if (recentUpload != null)
            {
                recentUpload.ContentId = content.Id ?? string.Empty;
                await _mongoDb.UpdateUploadAsync(recentUpload);
            }

            return Json(new { success = true, message = "Attachment replaced" });
        }

        public class ReplaceAssessmentAttachmentRequest
        {
            public string AssessmentId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminAssessment/DeleteAssessment")]
        public async Task<IActionResult> DeleteAssessment([FromBody] DeleteContentRequest req)
        {
            if (req == null || string.IsNullOrEmpty(req.AssessmentId)) return BadRequest(new { success = false, message = "Invalid request" });
            await _mongoDb.DeleteContentAsync(req.AssessmentId);
            await _mongoDb.DeleteUploadsByContentIdAsync(req.AssessmentId);
            return Json(new { success = true });
        }
    }
}
