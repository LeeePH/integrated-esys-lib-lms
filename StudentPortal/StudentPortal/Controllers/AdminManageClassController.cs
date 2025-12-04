using Microsoft.AspNetCore.Mvc;
using System.Linq;
using StudentPortal.Models.AdminDb;
using StudentPortal.Services;
using StudentPortal.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StudentPortal.Controllers.AdminDb
{
    [Route("AdminManageClass")]
    public class AdminManageClassController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public AdminManageClassController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        // ---------------- PAGE ----------------
        [HttpGet("Index/{classCode}")]
        public async Task<IActionResult> Index(string classCode)    
        {
            if (string.IsNullOrEmpty(classCode))
                return BadRequest("Class code is required.");

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound($"Class with code {classCode} not found.");

            var students = await _mongoDb.GetStudentsByClassCodeAsync(classItem.ClassCode);
            var attendance = await _mongoDb.GetAttendanceByClassCodeAsync(classItem.ClassCode);
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);
            var todayByStudent = attendance
                .Where(a => a.Date >= todayStart && a.Date < todayEnd)
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).First());

            foreach (var s in students)
            {
                if (todayByStudent.TryGetValue(s.Id, out var rec))
                {
                    s.Status = rec.Status;
                }
            }
            var joinRequests = await _mongoDb.GetJoinRequestsByClassCodeAsync(classItem.ClassCode);

            var vm = new AdminManageClassViewModel
            {
                ClassId = classItem.Id,
                SubjectName = classItem.SubjectName,
                SectionName = classItem.SectionLabel,
                ClassCode = classItem.ClassCode,
                Students = students,
                JoinRequests = joinRequests
            };

            return View("~/Views/AdminDb/AdminManageClass/Index.cshtml", vm);
        }

        // ---------------- JOIN REQUESTS ----------------
        [HttpPost("ApproveJoin")]
        public async Task<IActionResult> ApproveJoin([FromBody] ApproveJoinRequestModel req)
        {
            if (req == null || string.IsNullOrEmpty(req.RequestId))
                return BadRequest(new { Success = false, Message = "Invalid request." });

            var joinRequest = await _mongoDb.GetJoinRequestByIdAsync(req.RequestId);
            if (joinRequest == null)
                return NotFound(new { Success = false, Message = "Join request not found." });

            try
            {
                // Use joinRequest properties (more reliable than req properties)
                var studentEmail = !string.IsNullOrEmpty(req.StudentEmail) ? req.StudentEmail : joinRequest.StudentEmail;
                var classCode = !string.IsNullOrEmpty(req.ClassCode) ? req.ClassCode : joinRequest.ClassCode;

                if (string.IsNullOrEmpty(studentEmail))
                    return BadRequest(new { Success = false, Message = "Student email is required." });
                if (string.IsNullOrEmpty(classCode))
                    return BadRequest(new { Success = false, Message = "Class code is required." });

                // 1️⃣ Add student to class (updates JoinedClasses list)
                try
                {
                    await _mongoDb.AddStudentToClass(studentEmail, classCode);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { Success = false, Message = $"Could not save student to class: {ex.Message}" });
                }

                // 2️⃣ Update the join request status to "Approved"
                joinRequest.Status = "Approved";
                joinRequest.ApprovedAt = DateTime.UtcNow;
                await _mongoDb.UpdateJoinRequestAsync(joinRequest);

                return Ok(new
                {
                    success = true,
                    message = $"{joinRequest.StudentName} has been approved and added to the class."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Failed to approve request: {ex.Message}" });
            }
        }


        [HttpPost("RejectJoin")]
        public async Task<IActionResult> RejectJoin([FromBody] ApproveJoinRequestModel req)
        {
            if (req == null || string.IsNullOrEmpty(req.RequestId))
                return BadRequest(new { success = false, message = "Invalid request." });

            var joinRequest = await _mongoDb.GetJoinRequestByIdAsync(req.RequestId);
            if (joinRequest == null)
                return NotFound(new { success = false, message = "Join request not found." });

            await _mongoDb.RemoveJoinRequest(req.RequestId);

            return Ok(new
            {
                success = true,
                message = $"{joinRequest.StudentName}'s request was rejected."
            });
        }

        [HttpGet("GetJoinRequests/{classCode}")]
        public async Task<IActionResult> GetJoinRequests(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
                return BadRequest(new { success = false, message = "Class code is required." });

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            var sectionLabel = classItem?.SectionLabel ?? string.Empty;
            var requests = await _mongoDb.GetJoinRequestsByClassCodeAsync(classCode);

            // Return camelCase to align with frontend expectations
            var result = requests.Select(r => new
            {
                id = r.Id ?? string.Empty,
                studentName = r.StudentName ?? string.Empty,
                studentEmail = r.StudentEmail ?? string.Empty,
                classId = r.ClassId ?? string.Empty,
                classCode = r.ClassCode ?? string.Empty,
                sectionDisplay = sectionLabel,
                status = r.Status ?? "Pending",
                requestedAt = r.RequestedAt
            });

            return Ok(result);
        }

        // ---------------- STUDENTS (APPROVED) ----------------

        [HttpGet("GetStudentsByClassCode/{classCode}")]
        public async Task<IActionResult> GetStudentsByClassCode(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
                return BadRequest(new { success = false, message = "Class code is required." });

            var students = await _mongoDb.GetStudentsByClassCodeAsync(classCode);
            var attendance = await _mongoDb.GetAttendanceByClassCodeAsync(classCode);
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);
            var todayByStudent = attendance
                .Where(a => a.Date >= todayStart && a.Date < todayEnd)
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).First());

            // Return camelCase to align with frontend expectations
            var result = students.Select(s => new
            {
                id = s.Id ?? string.Empty,
                classId = s.ClassId ?? string.Empty,
                studentName = s.StudentName ?? string.Empty,
                studentEmail = s.StudentEmail ?? string.Empty,
                status = (todayByStudent.TryGetValue(s.Id, out var rec) ? rec.Status : s.Status) ?? string.Empty,
                grade = s.Grade
            });

            return Ok(result);
        }

        // ---------------- EXPORT (CSV) ----------------
        [HttpGet("Export/{classCode}")]
        public async Task<IActionResult> Export(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
                return BadRequest("Class code is required.");

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound($"Class with code {classCode} not found.");

            var students = await _mongoDb.GetStudentsByClassCodeAsync(classCode);
            var attendance = await _mongoDb.GetAttendanceByClassCodeAsync(classCode);
            var tasks = await _mongoDb.GetTasksByClassIdAsync(classItem.Id);

            // Build per-student task totals (points attained / total points)
            var taskTotals = new Dictionary<string, (double attained, double total)>();
            foreach (var s in students)
            {
                taskTotals[s.Id] = (0, 0);
            }
            foreach (var t in tasks)
            {
                var subs = await _mongoDb.GetTaskSubmissionsAsync(t.Id);
                foreach (var sub in subs)
                {
                    if (!taskTotals.ContainsKey(sub.StudentId)) continue;
                    var (a, tot) = taskTotals[sub.StudentId];
                    // Parse grade like "8/10" or "10/10"
                    var g = (sub.Grade ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(g) && g.Contains('/'))
                    {
                        var parts = g.Split('/');
                        if (parts.Length == 2 && double.TryParse(parts[0], out var got) && double.TryParse(parts[1], out var max) && max > 0)
                        {
                            a += got;
                            tot += max;
                        }
                    }
                    taskTotals[sub.StudentId] = (a, tot);
                }
            }

            // Attendance snapshot column for this export
            var exportTs = DateTime.UtcNow.ToLocalTime();
            var attLabel = $"Attendance_{exportTs:yyyy-MM-dd_HH-mm}";
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);
            var latestToday = attendance
                .Where(a => a.Date >= todayStart && a.Date < todayEnd)
                .GroupBy(a => a.StudentId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).FirstOrDefault()?.Status ?? "Absent");

            // Build CSV
            string esc(string s)
            {
                s ??= string.Empty;
                if (s.Contains('"') || s.Contains(',') || s.Contains('\n'))
                    return '"' + s.Replace("\"", "\"\"") + '"';
                return s;
            }

            var header = new[] { "Student_ID", "Last_Name", "First_Name", "Middle_Name", attLabel, "Task", "Assessment", "Standing" };
            var lines = new List<string> { string.Join(",", header) };

            foreach (var s in students)
            {
                var name = s.StudentName ?? string.Empty;
                var nameParts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var first = nameParts.Length > 0 ? nameParts[0] : string.Empty;
                var last = nameParts.Length > 1 ? nameParts[^1] : string.Empty;
                var middle = nameParts.Length > 2 ? string.Join(' ', nameParts.Skip(1).Take(nameParts.Length - 2)) : string.Empty;

                var attStatus = latestToday.TryGetValue(s.Id, out var st) ? st : (s.Status ?? string.Empty);
                if (string.IsNullOrWhiteSpace(attStatus)) attStatus = "Absent";

                var (attained, total) = taskTotals.TryGetValue(s.Id, out var tuple) ? tuple : (0, 0);
                var taskDisplay = total > 0 ? $"{attained}/{total}" : string.Empty;

                // Assessment left blank for manual input
                var assessmentDisplay = string.Empty;

                // Compute Standing using simple weights: Attendance 20%, Task 50%, Assessment 30%
                // Attendance percent from all records: Present=1, Late=0.5, Absent=0
                double attPct = 0;
                try
                {
                    var studentAtt = attendance.Where(a => a.StudentId == s.Id).ToList();
                    var totalRec = studentAtt.Count;
                    if (totalRec > 0)
                    {
                        double score = 0;
                        foreach (var ar in studentAtt)
                        {
                            var stNorm = (ar.Status ?? "").ToLowerInvariant();
                            if (stNorm == "present") score += 1;
                            else if (stNorm == "late") score += 0.5;
                        }
                        attPct = (score / totalRec) * 100.0;
                    }
                }
                catch { attPct = 0; }

                double taskPct = 0;
                if (total > 0) taskPct = (attained / total) * 100.0;
                double assessPct = 0; // blank

                var finalScore = attPct * 0.20 + taskPct * 0.50 + assessPct * 0.30;
                var standing = finalScore >= 75 ? "Passed" : "Failed";

                var row = new[]
                {
                    esc(s.Id ?? string.Empty),
                    esc(last),
                    esc(first),
                    esc(middle),
                    esc(attStatus),
                    esc(taskDisplay),
                    esc(assessmentDisplay),
                    esc(standing)
                };
                lines.Add(string.Join(",", row));
            }

            var csv = string.Join("\r\n", lines) + "\r\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            var fileName = $"Class_{classItem.ClassCode}_Export_{exportTs:yyyyMMdd_HHmm}.csv";
            return File(bytes, "text/csv", fileName);
        }

        // ---------------- ATTENDANCE ----------------
        public class MarkAttendanceRequest
        {
            public string StudentId { get; set; }
            public string Status { get; set; }
            public string ClassCode { get; set; }
        }

        [HttpPost("MarkAttendance")]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceRequest req)
        {
            if (req == null)
                return BadRequest(new { success = false, message = "Invalid request." });
            if (string.IsNullOrWhiteSpace(req.StudentId))
                return BadRequest(new { success = false, message = "StudentId is required." });
            if (string.IsNullOrWhiteSpace(req.Status))
                return BadRequest(new { success = false, message = "Status is required." });
            if (string.IsNullOrWhiteSpace(req.ClassCode))
                return BadRequest(new { success = false, message = "ClassCode is required." });

            // normalize status
            var statusNorm = req.Status.Trim();
            if (!(statusNorm.Equals("Present", StringComparison.OrdinalIgnoreCase) ||
                  statusNorm.Equals("Absent", StringComparison.OrdinalIgnoreCase) ||
                  statusNorm.Equals("Late", StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new { success = false, message = "Status must be Present, Absent, or Late." });
            }

            try
            {
                await _mongoDb.UpsertAttendanceRecordAsync(req.ClassCode, req.StudentId, statusNorm);
                return Ok(new { success = true, message = $"Marked {statusNorm} for today." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Failed to save attendance: {ex.Message}" });
            }
        }
    }
}
