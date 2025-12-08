using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminClass;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.AdminMaterial;
using MongoDB.Bson;
using StudentPortal.Models.AdminTask;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AdminClassController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public AdminClassController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/AdminClass/{id}")]
        public async Task<IActionResult> Index(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound("Class code not provided.");

            var classItem = await _mongoDb.GetClassByCodeAsync(id);
            if (classItem == null)
                return NotFound("Class not found.");

            var uploads = await _mongoDb.GetRecentUploadsByClassIdAsync(classItem.Id);
            var contents = await _mongoDb.GetContentsByClassIdAsync(classItem.Id);

            var vm = new AdminClassViewModel
            {
                ClassId = classItem.Id,
                SubjectName = classItem.SubjectName,
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                AdminName = User?.Identity?.Name ?? "Admin",
                AdminInitials = GetInitials(User?.Identity?.Name ?? "Admin"),
                RecentUploads = uploads.Select(u => new AdminClassRecentUpload
                {
                    IconClass = "fa-solid fa-file",
                    Title = u.FileName
                }).ToList(),
                Contents = contents.Select(c => new AdminClassContent
                {
                    Type = c.Type,
                    Title = c.Title,
                    IconClass = GetIconByType(c.Type),
                    MetaText = c.Type == "announcement"
                        ? $"{c.Description}\nBy {c.UploadedBy} | {c.CreatedAt:MMM d, yyyy h:mm tt}"
                        : $"Posted: {c.CreatedAt:MMM d, yyyy}",
                    TargetUrl = c.Type != "announcement" ? Url.Action("Index", $"Admin{Capitalize(c.Type)}", new { classCode = classItem.ClassCode, contentId = c.Id }) : null,
                    HasUrgency = c.HasUrgency,
                    UrgencyColor = c.UrgencyColor
                }).ToList()
            };

            return View("~/Views/AdminDb/AdminClass/Index.cshtml", vm);
        }

        [HttpPost("/AdminClass/AddAnnouncement")]
        public async Task<IActionResult> AddAnnouncement([FromBody] AnnouncementRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest("Announcement text cannot be empty.");

            var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassId);
            if (classItem == null)
                return NotFound("Class not found.");

            var announcement = new ContentItem
            {
                ClassId = classItem.Id,
                Type = "announcement",
                Title = "Announcement",
                Description = request.Text,
                UploadedBy = User?.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow,
                HasUrgency = false
            };

            await _mongoDb.InsertContentAsync(announcement);

            return Ok(new
            {
                announcement.Description,
                announcement.UploadedBy,
                announcement.CreatedAt
            });
        }

        [HttpPost("/AdminClass/CreateContent")]
        public async Task<IActionResult> CreateContent([FromBody] CreateContentRequest request)
        {
            try
            {
                var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassId);
                if (classItem == null)
                    return NotFound(new { success = false, message = "Class not found" });

                // Create content item
                var contentItem = new ContentItem
                {
                    ClassId = classItem.Id,
                    Title = request.Title,
                    Type = request.Type,
                    Description = request.Description,
                    MetaText = GenerateMetaText(request),
                    IconClass = GetIconClass(request.Type),
                    HasUrgency = !string.IsNullOrEmpty(request.Deadline),
                    UrgencyColor = GetUrgencyColor(request.Deadline),
                    CreatedAt = DateTime.UtcNow,
                    UploadedBy = User.Identity?.Name ?? "Admin",
                    Deadline = string.IsNullOrEmpty(request.Deadline) ? null : DateTime.Parse(request.Deadline),
                    LinkUrl = request.Link ?? string.Empty,
                    MaxGrade = request.MaxGrade
                };

                // Ensure content has an Id before linking uploads to it
                if (string.IsNullOrEmpty(contentItem.Id))
                {
                    contentItem.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
                }

                // If there's file information, update the upload record with ContentId
                if (!string.IsNullOrEmpty(request.FileName))
                {
                    // Find the most recent upload by this user for this class
                    var uploads = await _mongoDb.GetUploadsByClassIdAsync(classItem.Id);
                    var recentUpload = uploads
                        .Where(u => u.UploadedBy == (User.Identity?.Name ?? "Admin") && u.FileName == request.FileName)
                        .OrderByDescending(u => u.UploadedAt)
                        .FirstOrDefault();

                    if (recentUpload != null)
                    {
                        recentUpload.ContentId = contentItem.Id; // Link the upload to this content
                        await _mongoDb.UpdateUploadAsync(recentUpload);
                    }

                    // Also add to content item attachments
                    contentItem.Attachments.Add(request.FileName);
                }

                await _mongoDb.InsertContentAsync(contentItem);

                return Ok(new
                {
                    success = true,
                    message = $"{request.Type} created successfully",
                    contentId = contentItem.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Helper methods
        private static string GetIconByType(string type)
        {
            return type switch
            {
                "material" => "fa-solid fa-book-open-reader",
                "task" => "fa-solid fa-file-pen",
                "assessment" => "fa-solid fa-circle-question",
                _ => "fa-solid fa-file"
            };
        }

        private static string Capitalize(string str) =>
            string.IsNullOrEmpty(str) ? str : char.ToUpper(str[0]) + str.Substring(1);

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        private string GenerateMetaText(CreateContentRequest request)
        {
            var meta = $"Posted: {DateTime.UtcNow:MMM dd, yyyy}";

            if (!string.IsNullOrEmpty(request.Deadline))
                meta += $" | Deadline: {DateTime.Parse(request.Deadline):MMM dd, yyyy}";

            if (!string.IsNullOrEmpty(request.Description))
                meta += $" | {request.Description}";

            if (!string.IsNullOrEmpty(request.Link))
                meta += $" | Link: {request.Link}";

            if (!string.IsNullOrEmpty(request.FileName))
                meta += $" | File: {request.FileName}";

            return meta;
        }

        private string GetIconClass(string type) => type?.ToLower() switch
        {
            "material" => "fa-solid fa-book-open-reader",
            "task" => "fa-solid fa-file-pen",
            "assessment" => "fa-solid fa-circle-question",
            _ => "fa-solid fa-file"
        };

        private string GetUrgencyColor(string deadline)
        {
            if (string.IsNullOrEmpty(deadline)) return "yellow";

            var deadlineDate = DateTime.Parse(deadline);
            var daysUntil = (deadlineDate - DateTime.UtcNow).TotalDays;

            return daysUntil <= 2 ? "red" : daysUntil <= 7 ? "yellow" : "green";
        }

        // Request models
        public class AnnouncementRequest
        {
            public string ClassId { get; set; }
            public string Text { get; set; }
        }

        public class CreateContentRequest
        {
            public string Type { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public string Deadline { get; set; } = "";
            public string Link { get; set; } = "";
            public string ClassId { get; set; } = "";


            public string FileName { get; set; } = "";
            public string FileUrl { get; set; } = "";
            public long FileSize { get; set; }
            public int MaxGrade { get; set; } = 100;
        }

        [HttpPost("/AdminClass/UploadFile")]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { success = false, message = "No file uploaded" });

                // Get class information
                var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassCode);
                if (classItem == null)
                    return NotFound(new { success = false, message = "Class not found" });

                // Generate a unique file name
                var fileName = $"{Guid.NewGuid()}_{request.File.FileName}";
                var filePath = Path.Combine("wwwroot", "uploads", fileName);

                // Ensure uploads directory exists
                var uploadsDir = Path.Combine("wwwroot", "uploads");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                // Create file URL
                var fileUrl = $"/uploads/{fileName}";

                // Create upload record in database
                var uploadItem = new UploadItem
                {
                    ClassId = classItem.Id,
                    FileName = request.File.FileName,
                    FileUrl = fileUrl,
                    FileType = Path.GetExtension(request.File.FileName),
                    FileSize = request.File.Length,
                    UploadedBy = User?.Identity?.Name ?? "Admin",
                    UploadedAt = DateTime.UtcNow
                };

                await _mongoDb.InsertUploadAsync(uploadItem);

                return Ok(new
                {
                    success = true,
                    message = "File uploaded successfully",
                    fileName = request.File.FileName,
                    fileUrl = fileUrl,
                    fileSize = request.File.Length,
                    uploadId = uploadItem.Id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Add this request model to your AdminClassController
        public class UploadFileRequest
        {
            public string ClassCode { get; set; } = "";
            public string Type { get; set; } = "";
            public IFormFile File { get; set; }
        }
    }
}
