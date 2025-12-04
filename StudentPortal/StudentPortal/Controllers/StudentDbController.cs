using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models.Studentdb;
using StudentPortal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.Studentdb
{
    [Route("studentdb/[controller]")]
    public class StudentDbController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentDbController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        // GET: studentdb/StudentDb
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return Content("User is not logged in. Please log in first.");

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return Content("User not found in the database.");

            var joinedClasses = user.JoinedClasses ?? new List<string>();

            // Fetch pending join requests
            var pendingRequests = await _mongoDb.GetJoinRequestsByEmailAsync(email);
            var pendingClassCodes = pendingRequests
                                    .Where(r => r.Status == "Pending")
                                    .Select(r => r.ClassCode)
                                    .ToList();

            // Combine approved + pending
            var allClassCodes = joinedClasses.Concat(pendingClassCodes).Distinct().ToList();

            var classes = new List<ClassItem>();
            if (allClassCodes.Count > 0)
                classes = await _mongoDb.GetClassesByCodesAsync(allClassCodes); // CHANGED: GetClassesByCodesAsync instead of GetClassesByIdsAsync

            // Prepare view model
            var model = new StudentPortal.Models.Studentdb.AdminDashboardViewModel
            {
                UserName = string.IsNullOrWhiteSpace(user.FullName) ? "Student" : user.FullName.Trim(),
                Avatar = string.IsNullOrWhiteSpace(user.FullName)
                ? "ST"
                : string.Join("", user.FullName
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Take(2)          // take first 2 words
                    .Select(w => w[0])) // take first letter of each
                  .ToUpper(),
                CurrentPage = "home",
                Classes = classes.Select(c =>
                {
                    var status = joinedClasses.Contains(c.ClassCode) ? "Approved" : "Pending";
                    return new StudentPortal.Models.Studentdb.ClassContent
                    {
                        Title = string.IsNullOrWhiteSpace(c.SubjectName) ? "No title" : c.SubjectName,
                        Section = string.IsNullOrWhiteSpace(c.SectionLabel) ? "No section" : c.SectionLabel,
                        InstructorName = string.IsNullOrWhiteSpace(c.InstructorName) ? "Instructor" : c.InstructorName,
                        BackgroundImageUrl = string.IsNullOrWhiteSpace(c.BackgroundImageUrl) ? "/images/classbg.jpg" : c.BackgroundImageUrl,
                        ClassCode = c.ClassCode,
                        Status = status
                    };
                }).ToList()
            };

            return View("~/Views/Studentdb/StudentDb/Index.cshtml", model);
        }

        // POST: studentdb/StudentDb/RequestJoin
        [HttpPost("RequestJoin")]
        [Consumes("application/json")]
        public async Task<IActionResult> RequestJoin([FromBody] JoinClassRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ClassCode))
                return BadRequest(new { Success = false, Message = "Class code is required." });

            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
                return BadRequest(new { Success = false, Message = "User is not logged in." });

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
                return NotFound(new { Success = false, Message = "User not found." });

            var classItem = await _mongoDb.GetClassByCodeAsync(request.ClassCode.Trim());
            if (classItem == null)
                return NotFound(new { Success = false, Message = "Class not found." });

            // Check if already joined or pending
            if ((user.JoinedClasses?.Contains(classItem.ClassCode) ?? false) ||
                (await _mongoDb.JoinRequestExistsAsync(email, classItem.ClassCode)))
            {
                return BadRequest(new { Success = false, Message = "You have already joined or requested this class." });
            }

            // ✅ Create the join request
            var joinRequest = new JoinRequest
            {
                ClassId = classItem.Id,
                ClassCode = classItem.ClassCode,
                StudentEmail = user.Email,
                StudentName = string.IsNullOrWhiteSpace(user.FullName) ? "Student" : user.FullName,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
            };

            await _mongoDb.CreateJoinRequestAsync(joinRequest);

            // Return the created request so frontend can show/update
            return Ok(new { Success = true, Message = "Join request submitted! Waiting for admin approval.", JoinRequest = joinRequest });
        }
    }
}