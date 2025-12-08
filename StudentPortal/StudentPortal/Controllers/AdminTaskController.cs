using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminTask;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AdminTaskController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public AdminTaskController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/AdminTask/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            // Get content item from database
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "task")
                return NotFound("Task not found.");

            // Get class information using class code
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            // Verify that the content belongs to this class
            if (contentItem.ClassId != classItem.Id)
                return NotFound("Task not found in this class.");

            var submissions = await _mongoDb.GetTaskSubmissionsAsync(contentId);
            var vmSubmissions = submissions.Select(s => new TaskSubmission
            {
                Id = s.Id,
                FullName = s.StudentName,
                StudentEmail = s.StudentEmail,
                Submitted = s.Submitted,
                SubmittedAt = s.SubmittedAt,
                IsApproved = s.IsApproved,
                HasPassed = s.HasPassed,
                ApprovedDate = s.ApprovedDate,
                Grade = s.Grade,
                Feedback = s.Feedback,
                SubmittedFileName = s.FileName,
                SubmittedFileUrl = s.FileUrl,
                SubmittedFileSize = FormatFileSize(s.FileSize)
            }).ToList();

            var vm = new AdminTaskViewModel
            {
                TaskId = contentItem.Id,
                SubjectName = classItem.SubjectName,
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                InstructorName = User?.Identity?.Name ?? "Admin",
                InstructorInitials = GetInitials(User?.Identity?.Name ?? "Admin"),
                TaskTitle = contentItem.Title,
                TaskDescription = contentItem.Description,
                Attachments = contentItem.Attachments ?? new System.Collections.Generic.List<string>(),
                PostedDate = contentItem.CreatedAt,
                Deadline = contentItem.Deadline,
                Submissions = vmSubmissions,
                TaskMaxGrade = contentItem.MaxGrade
            };

            return View("~/Views/AdminDb/AdminTask/Index.cshtml", vm);
        }

        [HttpGet("/AdminTask/GetComments")]
        public async Task<IActionResult> GetComments([FromQuery] string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return BadRequest(new { success = false, message = "Missing taskId" });
            var items = await _mongoDb.GetTaskCommentsAsync(taskId);
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

        [HttpPost("/AdminTask/PostComment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(string taskId, string classCode, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Admin");
            var authorEmail = user?.Email ?? (email ?? "admin@local");
            var role = user?.Role ?? "Admin";
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null) return Json(new { success = false, message = "Class not found" });
            var item = await _mongoDb.AddTaskCommentAsync(taskId, classItem.Id, authorEmail, authorName, role, text ?? string.Empty);
            if (item == null) return Json(new { success = false, message = "Failed to add comment" });
            return Json(new { success = true, comment = new { id = item.Id, authorName = item.AuthorName, role = item.Role, text = item.Text, createdAt = item.CreatedAt, replies = item.Replies.Select(r => new { authorName = r.AuthorName, role = r.Role, text = r.Text, createdAt = r.CreatedAt }).ToList() } });
        }

        [HttpPost("/AdminTask/PostReply")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(string commentId, string text)
        {
            var email = HttpContext.Session.GetString("UserEmail");
            var user = !string.IsNullOrEmpty(email) ? await _mongoDb.GetUserByEmailAsync(email) : null;
            var authorName = user?.FullName ?? (User?.Identity?.Name ?? "Admin");
            var authorEmail = user?.Email ?? (email ?? "admin@local");
            var role = user?.Role ?? "Admin";
            var updated = await _mongoDb.AddTaskReplyAsync(commentId, authorEmail, authorName, role, text ?? string.Empty);
            if (updated == null) return Json(new { success = false, message = "Failed to add reply" });
            var last = updated.Replies.LastOrDefault();
            return Json(new { success = true, reply = last != null ? new { authorName = last.AuthorName, role = last.Role, text = last.Text, createdAt = last.CreatedAt } : null });
        }

        [HttpPost("/AdminTask/UpdateTask")]
        public async Task<IActionResult> UpdateTask([FromBody] UpdateTaskRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                // Update task properties
                contentItem.Title = request.Title;
                contentItem.Description = request.Description;
                contentItem.Deadline = request.Deadline;

                // Update meta text to reflect changes
                contentItem.MetaText = GenerateUpdatedMetaText(contentItem);

                await _mongoDb.UpdateContentAsync(contentItem);

                return Ok(new { success = true, message = "Task updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminTask/DeleteTask")]
        public async Task<IActionResult> DeleteTask([FromBody] DeleteTaskRequest request)
        {
            try
            {
                await _mongoDb.DeleteContentAsync(request.TaskId);
                await _mongoDb.DeleteUploadsByContentIdAsync(request.TaskId);
                return Ok(new { success = true, message = "Task deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminTask/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceTaskAttachmentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TaskId) || string.IsNullOrEmpty(request.FileName))
                    return BadRequest(new { success = false, message = "Invalid request" });

                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                contentItem.Attachments = new System.Collections.Generic.List<string> { request.FileName };
                await _mongoDb.UpdateContentAsync(contentItem);

                var uploads = await _mongoDb.GetUploadsByClassIdAsync(contentItem.ClassId);
                var recentUpload = uploads
                    .Where(u => u.UploadedBy == (User?.Identity?.Name ?? "Admin") && u.FileName == request.FileName)
                    .OrderByDescending(u => u.UploadedAt)
                    .FirstOrDefault();

                if (recentUpload != null)
                {
                    recentUpload.ContentId = contentItem.Id ?? string.Empty;
                    await _mongoDb.UpdateUploadAsync(recentUpload);
                }

                return Ok(new { success = true, message = "Attachment replaced" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class ReplaceTaskAttachmentRequest
        {
            public string TaskId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminTask/AddAttachment")]
        public async Task<IActionResult> AddAttachment([FromBody] AddAttachmentRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.TaskId);
                if (contentItem == null || contentItem.Type != "task")
                    return NotFound(new { success = false, message = "Task not found" });

                if (!contentItem.Attachments.Contains(request.FileName))
                {
                    contentItem.Attachments.Add(request.FileName);
                    await _mongoDb.UpdateContentAsync(contentItem);
                }

                return Ok(new { success = true, message = "Attachment added successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/AdminTask/GetSubmission")]        
        public async Task<IActionResult> GetSubmission([FromQuery] string submissionId)
        {
            if (string.IsNullOrEmpty(submissionId)) return BadRequest(new { success = false, message = "Missing submissionId" });
            var sub = await _mongoDb.GetSubmissionByIdAsync(submissionId);
            if (sub == null) return NotFound(new { success = false, message = "Submission not found" });
            return Json(new
            {
                success = true,
                submission = new
                {
                    id = sub.Id,
                    studentName = sub.StudentName,
                    studentEmail = sub.StudentEmail,
                    submitted = sub.Submitted,
                    submittedAt = sub.SubmittedAt,
                    isApproved = sub.IsApproved,
                    hasPassed = sub.HasPassed,
                    approvedDate = sub.ApprovedDate,
                    grade = sub.Grade,
                    feedback = sub.Feedback,
                    fileName = sub.FileName,
                    fileUrl = sub.FileUrl,
                    fileSize = sub.FileSize
                }
            });
        }

        [HttpPost("/AdminTask/GradeSubmission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GradeSubmission(string submissionId, string grade, string feedback, bool? approve, bool? pass)
        {
            if (string.IsNullOrEmpty(submissionId)) return BadRequest(new { success = false, message = "Missing submissionId" });
            var isApproved = approve ?? true;

            var submission = await _mongoDb.GetSubmissionByIdAsync(submissionId);
            if (submission == null) return NotFound(new { success = false, message = "Submission not found" });
            var content = !string.IsNullOrEmpty(submission.TaskId) ? await _mongoDb.GetContentByIdAsync(submission.TaskId) : null;
            var maxGrade = content?.MaxGrade ?? 100;

            var gtext = (grade ?? string.Empty).Trim();
            string finalGrade = gtext;
            if (!string.IsNullOrEmpty(gtext))
            {
                if (gtext.Contains('/'))
                {
                    finalGrade = gtext;
                }
                else if (gtext.EndsWith("%"))
                {
                    var pctText = gtext.Substring(0, gtext.Length - 1);
                    if (double.TryParse(pctText, out var pct))
                    {
                        var got = Math.Round(maxGrade * (pct / 100.0), 2);
                        finalGrade = $"{got}/{maxGrade}";
                    }
                }
                else if (double.TryParse(gtext, out var gotNum))
                {
                    finalGrade = $"{gotNum}/{maxGrade}";
                }
            }

            bool computedPass = false;
            if (!string.IsNullOrEmpty(finalGrade) && finalGrade.Contains('/'))
            {
                var parts = finalGrade.Split('/');
                if (parts.Length == 2 && double.TryParse(parts[0], out var got) && double.TryParse(parts[1], out var max) && max > 0)
                {
                    var pct = (got / max) * 100.0;
                    computedPass = pct >= 75;
                }
            }
            else if (double.TryParse(gtext.TrimEnd('%'), out var pctNum))
            {
                computedPass = pctNum >= 75;
            }

            var hasPassed = pass ?? computedPass;
            var ok = await _mongoDb.UpdateSubmissionStatusAsync(submissionId, isApproved, hasPassed, finalGrade ?? string.Empty, feedback ?? string.Empty);
            if (!ok) return StatusCode(500, new { success = false, message = "Failed to update submission" });
            return Json(new { success = true });
        }

        private string GenerateUpdatedMetaText(ContentItem content)
        {
            var meta = $"Posted: {content.CreatedAt:MMM dd, yyyy}";

            if (content.Deadline.HasValue)
                meta += $" | Deadline: {content.Deadline.Value:MMM dd, yyyy}";

            if (!string.IsNullOrEmpty(content.Description))
                meta += $" | {content.Description}";

            return meta;
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            var kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:0.#} KB";
            var mb = kb / 1024d;
            return $"{mb:0.#} MB";
        }

        // Request models
        public class UpdateTaskRequest
        {
            public string TaskId { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime? Deadline { get; set; }
        }

        public class DeleteTaskRequest
        {
            public string TaskId { get; set; } = "";
        }

        public class AddAttachmentRequest
        {
            public string TaskId { get; set; } = "";
            public string FileName { get; set; } = "";
        }

        [HttpPost("/AdminTask/UpdateSubmissionStatus")]
        public async Task<IActionResult> UpdateSubmissionStatus([FromBody] UpdateSubmissionStatusRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentId))
                    return BadRequest(new { success = false, message = "Missing submission identifier" });

                var ok = await _mongoDb.UpdateSubmissionStatusAsync(
                    request.StudentId,
                    request.IsApproved,
                    request.HasPassed,
                    request.Grade ?? string.Empty,
                    request.Feedback ?? string.Empty
                );

                if (!ok)
                    return NotFound(new { success = false, message = "Submission not found" });

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class UpdateSubmissionStatusRequest
        {
            public string StudentId { get; set; } = ""; // actually submissionId from UI
            public string TaskId { get; set; } = ""; // optional, unused for submissionId update
            public bool IsApproved { get; set; }
            public bool HasPassed { get; set; }
            public string? Grade { get; set; }
            public string? Feedback { get; set; }
        }

        [HttpGet("/AdminTask/GetSubmissionCounts")]
        public async Task<IActionResult> GetSubmissionCounts([FromQuery] string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
                return BadRequest(new { success = false, message = "Missing taskId" });

            var total = await _mongoDb.GetSubmissionCountAsync(taskId, submittedOnly: false);
            var submitted = await _mongoDb.GetSubmissionCountAsync(taskId, submittedOnly: true);
            var approved = await _mongoDb.GetApprovedSubmissionCountAsync(taskId);

            return Ok(new { success = true, submittedCount = submitted, approvedCount = approved, totalCount = total });
        }
    }
}
