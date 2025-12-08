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

            // Build display name from FirstName, MiddleName, LastName, or fallback to FullName
            string displayName = "Student";
            var nameParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(user.FirstName)) nameParts.Add(user.FirstName);
            if (!string.IsNullOrWhiteSpace(user.MiddleName)) nameParts.Add(user.MiddleName);
            if (!string.IsNullOrWhiteSpace(user.LastName)) nameParts.Add(user.LastName);
            
            if (nameParts.Count > 0)
            {
                displayName = string.Join(" ", nameParts);
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                displayName = user.FullName.Trim();
            }
            
            // Build initials from name parts
            string initials = "ST";
            if (nameParts.Count > 0)
            {
                // Use first letter of first name and last name
                var firstInitial = nameParts[0][0].ToString().ToUpper();
                var lastInitial = nameParts.Count > 1 ? nameParts[nameParts.Count - 1][0].ToString().ToUpper() : "";
                initials = firstInitial + lastInitial;
            }
            else if (!string.IsNullOrWhiteSpace(user.FullName))
            {
                var fullNameParts = user.FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (fullNameParts.Length > 0)
                {
                    initials = string.Join("", fullNameParts.Take(2).Select(w => w[0])).ToUpper();
                }
            }
            
            // Prepare view model
            var model = new StudentPortal.Models.Studentdb.AdminDashboardViewModel
            {
                UserName = displayName,
                Avatar = initials,
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