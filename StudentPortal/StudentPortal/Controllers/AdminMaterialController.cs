using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminMaterial;
using StudentPortal.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class AdminMaterialController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public AdminMaterialController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("/AdminMaterial/{classCode}/{contentId}")]
        public async Task<IActionResult> Index(string classCode, string contentId)
        {
            if (string.IsNullOrEmpty(classCode) || string.IsNullOrEmpty(contentId))
                return NotFound("Class code or Content ID not provided.");

            // Get content item from database
            var contentItem = await _mongoDb.GetContentByIdAsync(contentId);
            if (contentItem == null || contentItem.Type != "material")
                return NotFound("Material not found.");

            // Get class information using class code
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound("Class not found.");

            // Verify that the content belongs to this class
            if (contentItem.ClassId != classItem.Id)
                return NotFound("Material not found in this class.");

            // Get recent materials for this class
            var recentMaterials = await _mongoDb.GetRecentMaterialsByClassIdAsync(classItem.Id);

            // Get uploaded files for this material from Uploads collection - USING ContentId now
            var uploadedFiles = await _mongoDb.GetUploadsByContentIdAsync(contentId);

            var vm = new AdminMaterialViewModel
            {
                MaterialId = contentItem.Id,
                SubjectName = classItem.SubjectName,
                SubjectCode = classItem.SubjectCode,
                ClassCode = classItem.ClassCode,
                InstructorName = User?.Identity?.Name ?? "Admin",
                InstructorInitials = GetInitials(User?.Identity?.Name ?? "Admin"),
                MaterialName = contentItem.Title,
                MaterialDescription = contentItem.Description,
                Attachments = uploadedFiles.Select(u => u.FileName).ToList(), // Use actual uploaded files
                PostedDate = contentItem.CreatedAt.ToString("MMM d, yyyy"),
                RecentMaterials = recentMaterials
            };

            return View("~/Views/AdminDb/AdminMaterial/Index.cshtml", vm);
        }

        [HttpPost("/AdminMaterial/ReplaceAttachment")]
        public async Task<IActionResult> ReplaceAttachment([FromBody] ReplaceAttachmentRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.MaterialId) || string.IsNullOrEmpty(request.FileName))
                    return BadRequest(new { success = false, message = "Invalid request" });

                var contentItem = await _mongoDb.GetContentByIdAsync(request.MaterialId);
                if (contentItem == null || contentItem.Type != "material")
                    return NotFound(new { success = false, message = "Material not found" });

                // Replace attachments with the new file name
                contentItem.Attachments = new System.Collections.Generic.List<string> { request.FileName };
                await _mongoDb.UpdateContentAsync(contentItem);

                // Link the most recent upload record to this content
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

        public class ReplaceAttachmentRequest
        {
            public string MaterialId { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
            public string? FileUrl { get; set; }
        }

        [HttpPost("/AdminMaterial/UpdateMaterial")]
        public async Task<IActionResult> UpdateMaterial([FromBody] UpdateMaterialRequest request)
        {
            try
            {
                var contentItem = await _mongoDb.GetContentByIdAsync(request.MaterialId);
                if (contentItem == null || contentItem.Type != "material")
                    return NotFound(new { success = false, message = "Material not found" });

                // Update material properties
                contentItem.Title = request.Title;
                contentItem.Description = request.Description;

                await _mongoDb.UpdateContentAsync(contentItem);

                return Ok(new { success = true, message = "Material updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("/AdminMaterial/DeleteMaterial")]
        public async Task<IActionResult> DeleteMaterial([FromBody] DeleteMaterialRequest request)
        {
            try
            {
                await _mongoDb.DeleteContentAsync(request.MaterialId);
                await _mongoDb.DeleteUploadsByContentIdAsync(request.MaterialId);
                return Ok(new { success = true, message = "Material deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("/AdminMaterial/DownloadFile/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName, string contentId)
        {
            try
            {
                // Get the file record from database
                var uploadItem = await _mongoDb.GetUploadByFileNameAsync(fileName, contentId);
                if (uploadItem == null)
                    return NotFound("File not found.");

                // Here you would typically:
                // 1. Get the actual file from your file storage (Azure Blob, AWS S3, or wwwroot/uploads)
                // 2. Return the file as a FileResult

                // For now, return a placeholder
                return Ok(new { success = true, message = "Download functionality to be implemented", fileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        private static string GetInitials(string fullName)
        {
            var parts = fullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            return string.Concat(parts.Select(p => p[0])).ToUpper();
        }

        // Request models
        public class UpdateMaterialRequest
        {
            public string MaterialId { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
        }

        public class DeleteMaterialRequest
        {
            public string MaterialId { get; set; } = "";
        }
    }
}
