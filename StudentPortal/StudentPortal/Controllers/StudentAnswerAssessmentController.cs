using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using StudentPortal.Models.AdminDb;
using System.Threading.Tasks;

namespace SIA_IPT.Controllers
{
    public class StudentAnswerAssessmentController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentAnswerAssessmentController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/StudentAnswerAssessment/{classCode}/{contentId}")]
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

            var vm = new StudentAnswerAssessmentViewModel
            {
                StudentName = user?.FullName ?? "Student",
                StudentInitials = GetInitials(user?.FullName ?? "ST"),
                SubjectName = classItem.SubjectName ?? string.Empty,
                AssessmentTitle = contentItem.Title ?? "Assessment",
                FormUrl = !string.IsNullOrWhiteSpace(contentItem.LinkUrl) ? NormalizeEmbedUrl(contentItem.LinkUrl) : string.Empty,
                Questions = new System.Collections.Generic.List<AssessmentQuestion>()
            };

            return View("~/Views/StudentDb/StudentAnswerAssessment/Index.cshtml", vm);
        }


        [HttpPost("/StudentAnswerAssessment/{classCode}/{contentId}/log-event")]
        public async Task<IActionResult> LogEvent(string classCode, string contentId, [FromBody] AntiCheatLogRequest req)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return BadRequest("Missing identifiers.");

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null)
                return NotFound();

            var log = new AntiCheatLog
            {
                ClassId = classItem.Id,
                ClassCode = classItem.ClassCode,
                ContentId = contentItem.Id,
                StudentId = user.Id ?? string.Empty,
                StudentEmail = user.Email ?? string.Empty,
                StudentName = user.FullName ?? string.Empty,
                ResultId = req.ResultId ?? string.Empty,
                EventType = req.EventType ?? string.Empty,
                Details = req.Details ?? string.Empty,
                EventCount = req.EventCount,
                EventDuration = req.EventDuration,
                Severity = string.IsNullOrWhiteSpace(req.Severity) ? "low" : req.Severity,
                Flagged = req.Flagged,
                LogTimeUtc = DateTime.UtcNow
            };

            await _mongoDb.AddAntiCheatLogAsync(log);
            return Ok(new { status = "event_logged" });
        }

        [HttpPost("/StudentAnswerAssessment/{classCode}/{contentId}/mark-answered")]
        public async Task<IActionResult> MarkAnswered(string classCode, string contentId)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email)) return Unauthorized();
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (classItem == null || contentItem == null || user == null) return NotFound();
            await _mongoDb.UpsertAssessmentSubmittedAsync(classItem.Id, classItem.ClassCode, contentItem.Id, user.Id ?? string.Empty, user.Email ?? string.Empty);
            return Ok(new { status = "marked_submitted" });
        }

        private string NormalizeEmbedUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                var u = new System.Uri(url);
                var host = u.Host.ToLowerInvariant();
                var path = u.AbsolutePath;

                // Google Forms
                if (host.Contains("docs.google.com") && path.Contains("/forms/"))
                {
                    var baseUrl = url.Split('#')[0];
                    if (!baseUrl.Contains("embedded=true"))
                    {
                        baseUrl += (baseUrl.Contains("?") ? "&" : "?") + "embedded=true";
                    }
                    return baseUrl;
                }

                // Google Docs Document
                if (host.Contains("docs.google.com") && path.Contains("/document/d/"))
                {
                    if (!path.EndsWith("/preview"))
                    {
                        return $"https://docs.google.com{path}/preview";
                    }
                    return url;
                }

                // Google Sheets
                if (host.Contains("docs.google.com") && path.Contains("/spreadsheets/d/"))
                {
                    // Use pubhtml widget for embeddable view
                    var docId = path.Split('/').FirstOrDefault(p => p.Length > 20);
                    if (!string.IsNullOrEmpty(docId))
                    {
                        return $"https://docs.google.com/spreadsheets/d/{docId}/pubhtml?widget=true&headers=false";
                    }
                }

                // Google Slides
                if (host.Contains("docs.google.com") && path.Contains("/presentation/d/"))
                {
                    var docId = path.Split('/').FirstOrDefault(p => p.Length > 20);
                    if (!string.IsNullOrEmpty(docId))
                    {
                        return $"https://docs.google.com/presentation/d/{docId}/embed?start=false&loop=false&delayms=3000";
                    }
                }

                // Google Drive file preview
                if (host.Contains("drive.google.com") && path.Contains("/file/d/"))
                {
                    var segments = path.Split('/');
                    var idIndex = System.Array.IndexOf(segments, "d");
                    if (idIndex >= 0 && idIndex + 1 < segments.Length)
                    {
                        var id = segments[idIndex + 1];
                        return $"https://drive.google.com/file/d/{id}/preview";
                    }
                }

                return url;
            }
            catch
            {
                return url;
            }
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }

        // POST: /StudentAnswerAssessment/SubmitAssessment  (form post)
        [HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult SubmitAssessment([FromForm] StudentAnswerSubmission submission)
		{
			if (submission == null)
			{
				TempData["ToastMessage"] = "Submission failed: payload missing.";
				return RedirectToAction("Index");
			}

			// TODO: replace with real persistence logic (DB, queue, etc.)
			Console.WriteLine($"[Assessment Submit] StudentId: {submission.StudentId ?? "anonymous"} - Received {submission.Answers?.Count ?? 0} answers at {DateTime.UtcNow:o}");
			if (!string.IsNullOrEmpty(submission.AntiCheatSummary))
			{
				Console.WriteLine($"[AntiCheat Summary] {submission.AntiCheatSummary}");
			}

			TempData["ToastMessage"] = "? Assessment submitted successfully!";
			return RedirectToAction("Index");
		}

		// POST: /StudentAnswerAssessment/SubmitJson  (ajax JSON)
		[HttpPost("StudentAnswerAssessment/SubmitJson")]
		[Consumes("application/json")]
		public IActionResult SubmitAssessmentJson([FromBody] StudentAnswerSubmission submission)
		{
			if (submission == null)
			{
				return BadRequest(new { success = false, message = "payload missing" });
			}

			// TODO: persist submission (DB, storage). For now log to console.
			Console.WriteLine($"[Assessment SubmitJson] StudentId: {submission.StudentId ?? "anonymous"} - {submission.Answers?.Count ?? 0} answers");
			if (!string.IsNullOrEmpty(submission.AntiCheatSummary))
			{
				Console.WriteLine($"[AntiCheat Summary] {submission.AntiCheatSummary}");
			}

			// Return a JSON success payload
			return Ok(new { success = true, message = "Assessment received" });
		}

		// POST: /StudentAnswerAssessment/ReportAntiCheat
		// Accepts larger anti-cheat logs if the client wants to offload them to server
		[HttpPost("StudentAnswerAssessment/ReportAntiCheat")]
		[Consumes("application/json")]
		public IActionResult ReportAntiCheat([FromBody] AntiCheatReport report)
		{
			if (report == null)
			{
				return BadRequest(new { success = false, message = "report missing" });
			}

			// TODO: persist or forward the anti-cheat log; for now log to console
			Console.WriteLine($"[AntiCheat Report] source={report.Source} received {report.Events?.Count ?? 0} events at {DateTime.UtcNow:o}");
			return Ok(new { success = true });
		}
	}
}
