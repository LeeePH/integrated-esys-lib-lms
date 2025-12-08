using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class StudentClassController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentClassController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("StudentClass/{classCode}")]
        public async Task<IActionResult> Index(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
            {
                return RedirectToAction("Index", "StudentDb");
            }

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return RedirectToAction("Index", "StudentDb");

            // Get class details from database
            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return Content("Class not found.");

            // Get user info
            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Content("User not found.");

            // Get ALL content items for this class from database
            var contentItems = await _mongoDb.GetContentsByClassIdAsync(classItem.Id);

            // Transform database content to your view model format - USING ACTUAL DATABASE TYPES
            var contentCards = new List<ContentCard>();

            foreach (var content in contentItems.OrderByDescending(c => c.CreatedAt))
            {
                // Use the actual Type from database (material, task, assessment, announcement)
                string contentType = content.Type?.ToLower() ?? "material";

                // Determine target action based on ACTUAL content type from database
                string targetAction = contentType switch
                {
                    "material" => $"/StudentMaterial/{classItem.ClassCode}/{content.Id}",
                    "task" => $"/StudentTask/{classItem.ClassCode}/{content.Id}",
                    "assessment" => $"/StudentAssessment/{classItem.ClassCode}/{content.Id}",
                    "announcement" => $"/StudentAnnouncement/{classItem.ClassCode}/{content.Id}",
                    _ => null
                };

                // Use actual urgency settings from database
                string urgency = content.HasUrgency ? content.UrgencyColor : null;

                contentCards.Add(new ContentCard
                {
                    Type = contentType, // USE ACTUAL DATABASE TYPE
                    Title = content.Title,
                    Meta = !string.IsNullOrEmpty(content.MetaText)
                           ? content.MetaText
                           : $"Posted: {content.CreatedAt:MMM dd, yyyy}",
                    TargetAction = targetAction,
                    Urgency = urgency // USE ACTUAL DATABASE URGENCY
                });
            }

            var viewModel = new StudentClassViewModel
            {
                SubjectName = classItem.SubjectName ?? "Computer Science 101",
                SubjectCode = classItem.SubjectCode ?? "CS101",
                ClassCode = classItem.ClassCode ?? classCode,
                InstructorName = classItem.InstructorName ?? "John Doe",
                InstructorInitials = GetInitials(classItem.InstructorName ?? "John Doe"),
                UserName = user.FullName ?? "Von GPT",
                Avatar = GetAvatarInitials(user.FullName ?? "Von GPT"),
                Contents = contentCards
            };

            return View("~/Views/StudentDb/StudentClass/Index.cshtml", viewModel);
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "JD";

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            else if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpper();
            else
                return "JD";
        }

        private string GetAvatarInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "VG";

            var parts = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[parts.Length - 1][0]}".ToUpper();
            else if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0].Substring(0, 2).ToUpper();
            else
                return "VG";
        }

        // --- ROUTES for specific content pages ---
        public IActionResult Material()
        {
            var vm = new StudentMaterialViewModel
            {
                SubjectName = "Computer Science 101",
                MaterialTitle = "Week 1: Introduction to Computing",
                Description = "This material introduces the fundamental concepts of computing, including the history of computers, types of systems, and basic principles of software and hardware interaction.",
                UploadedBy = "John Doe",
                UploadDate = System.DateTime.UtcNow,
                Files = new List<MaterialFile>
                {
                    new MaterialFile { FileName = "Week1_Introduction.pdf", FileUrl = "/files/Week1_Introduction.pdf" },
                    new MaterialFile { FileName = "Week1_Slides.pptx", FileUrl = "/files/Week1_Slides.pptx" }
                }
            };
            return View("~/Views/StudentDb/StudentMaterial/Index.cshtml", vm);
        }

        public IActionResult Task()
        {
            var vm = new StudentTaskViewModel
            {
                SubjectName = "Computer Science 101",
                TaskTitle = "Task 1: Intro Essay",
                Description = "Write an essay introducing yourself and your interests in the field of Computer Science.",
                PostedDate = System.DateTime.UtcNow.ToString("MMM d, yyyy"),
                Deadline = System.DateTime.UtcNow.AddDays(7).ToString("MMM d, yyyy"),
                StudentName = "Von GPT",
                StudentInitials = "VG",
                Attachments = new List<TaskAttachment>
                {
                    new TaskAttachment { FileName = "TaskInstructions.pdf", FileUrl = "/files/TaskInstructions.pdf" }
                }
            };
            return View("~/Views/StudentDb/StudentTask/Index.cshtml", vm);
        }
        public IActionResult Assessment() => View("~/Views/StudentDb/StudentAssessment/Index.cshtml");

        // --- BACK / DASHBOARD NAVIGATION ---
        public IActionResult ToDashboard()
        {
            return RedirectToAction("Index", "StudentDb");
        }
    }
}
