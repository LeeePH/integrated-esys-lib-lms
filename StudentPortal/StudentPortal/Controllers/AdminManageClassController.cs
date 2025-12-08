using Microsoft.AspNetCore.Mvc;
using System.Linq;
using StudentPortal.Models.AdminDb;
using StudentPortal.Models;
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

        // ---------------- EXPORT DATA (JSON for Excel) ----------------
        [HttpGet("ExportData/{classCode}")]
        public async Task<IActionResult> ExportData(string classCode)
        {
            if (string.IsNullOrEmpty(classCode))
                return BadRequest(new { success = false, message = "Class code is required." });

            var classItem = await _mongoDb.GetClassByCodeAsync(classCode);
            if (classItem == null)
                return NotFound(new { success = false, message = $"Class with code {classCode} not found." });

            var students = await _mongoDb.GetStudentsByClassCodeAsync(classCode);
            var attendance = await _mongoDb.GetAttendanceByClassCodeAsync(classCode);
            
            // Get Content items where Type="task" instead of TaskItem
            // This ensures we get tasks with Title and Type from Content collection
            var allContentItems = await _mongoDb.GetContentsByClassIdAsync(classItem.Id);
            var taskContents = allContentItems.Where(c => c.Type?.ToLower() == "task").ToList();

            // FIRST: Collect ALL submissions for all tasks to find students who have submissions
            // Submissions reference ContentItem.Id (not TaskItem.Id)
            var allSubmissions = new List<Submission>();
            foreach (var content in taskContents)
            {
                var subs = await _mongoDb.GetTaskSubmissionsAsync(content.Id ?? string.Empty);
                allSubmissions.AddRange(subs);
            }

            // Extract unique student identifiers from submissions (by email and StudentId)
            var submissionStudentEmails = allSubmissions
                .Where(s => !string.IsNullOrWhiteSpace(s.StudentEmail))
                .Select(s => s.StudentEmail.Trim().ToLowerInvariant())
                .Distinct()
                .ToHashSet();

            var submissionStudentIds = allSubmissions
                .Where(s => !string.IsNullOrWhiteSpace(s.StudentId))
                .Select(s => s.StudentId)
                .Distinct()
                .ToHashSet();

            // Ensure all students with submissions are in the students list
            var studentsDict = students.ToDictionary(s => s.Id, s => s);
            var studentsByEmail = students.Where(s => !string.IsNullOrEmpty(s.StudentEmail))
                .ToDictionary(s => s.StudentEmail.Trim().ToLowerInvariant(), s => s, StringComparer.OrdinalIgnoreCase);

            // Add students from submissions who aren't in the class list
            foreach (var email in submissionStudentEmails)
            {
                if (!studentsByEmail.ContainsKey(email))
                {
                    // Try to find user by email to get their info
                    try
                    {
                        var user = await _mongoDb.GetUserByEmailAsync(email);
                        if (user != null)
                        {
                            // Create a StudentRecord for this student
                            var newStudent = new StudentRecord
                            {
                                Id = user.Id ?? Guid.NewGuid().ToString(),
                                ClassId = classItem.Id,
                                StudentName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : 
                                    $"{user.FirstName} {user.MiddleName} {user.LastName}".Trim(),
                                StudentEmail = email,
                                Status = "Active",
                                Grade = 0.0
                            };
                            students.Add(newStudent);
                            studentsByEmail[email] = newStudent;
                            studentsDict[newStudent.Id] = newStudent;
                        }
                    }
                    catch
                    {
                        // If we can't find user, create a minimal StudentRecord from submission data
                        var submissionForEmail = allSubmissions.FirstOrDefault(s => 
                            string.Equals(s.StudentEmail, email, StringComparison.OrdinalIgnoreCase));
                        if (submissionForEmail != null)
                        {
                            var newStudent = new StudentRecord
                            {
                                Id = submissionForEmail.StudentId ?? Guid.NewGuid().ToString(),
                                ClassId = classItem.Id,
                                StudentName = submissionForEmail.StudentName ?? string.Empty,
                                StudentEmail = email,
                                Status = "Active",
                                Grade = 0.0
                            };
                            students.Add(newStudent);
                            studentsByEmail[email] = newStudent;
                            studentsDict[newStudent.Id] = newStudent;
                        }
                    }
                }
            }

            // Build per-student task totals (points attained / total points)
            var taskTotals = new Dictionary<string, (double attained, double total)>();
            var studentIdMap = new Dictionary<string, string>(); // Map StudentRecord.Id to StudentEmail for matching
            var studentIdentifiers = new Dictionary<string, HashSet<string>>(); // Map StudentRecord.Id to all possible identifiers
            
            // Build comprehensive student identifier mapping
            foreach (var s in students)
            {
                taskTotals[s.Id] = (0, 0);
                var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                identifiers.Add(s.Id);
                if (!string.IsNullOrEmpty(s.StudentEmail))
                {
                    identifiers.Add(s.StudentEmail);
                    studentIdMap[s.Id] = s.StudentEmail;
                }
                // Also add StudentId from submissions if it matches
                if (submissionStudentIds.Contains(s.Id))
                {
                    identifiers.Add(s.Id);
                }
                studentIdentifiers[s.Id] = identifiers;
            }
            
            // Also get enrollment student usernames for matching
            foreach (var s in students)
            {
                if (string.IsNullOrEmpty(s.StudentEmail)) continue;
                try
                {
                    var enrollmentStudent = await _mongoDb.GetEnrollmentStudentByEmailAsync(s.StudentEmail);
                    if (enrollmentStudent != null && !string.IsNullOrEmpty(enrollmentStudent.Username))
                    {
                        if (studentIdentifiers.ContainsKey(s.Id))
                        {
                            studentIdentifiers[s.Id].Add(enrollmentStudent.Username);
                        }
                    }
                }
                catch
                {
                    // Continue if enrollment lookup fails
                }
            }
            
            var maxGradeCache = new Dictionary<string, int>();
            var gradeList = new Dictionary<string, List<string>>();
            
            // Create a dictionary to store task titles by Content ID (which is what Submission.TaskId references)
            var taskTitles = new Dictionary<string, string>();
            var taskTypes = new Dictionary<string, string>();
            foreach (var content in taskContents)
            {
                var contentId = content.Id ?? string.Empty;
                taskTitles[contentId] = content.Title ?? string.Empty;
                taskTypes[contentId] = content.Type ?? string.Empty;
            }
            
            // Create a dictionary to store per-student per-task grades
            var studentTaskGrades = new Dictionary<string, Dictionary<string, string>>();
            foreach (var s in students)
            {
                studentTaskGrades[s.Id] = new Dictionary<string, string>();
            }
            
            // Collect all grades per task per student using the submissions we already collected
            // Group submissions by task (TaskId in Submission refers to ContentItem.Id)
            var submissionsByTask = allSubmissions.GroupBy(s => s.TaskId).ToDictionary(g => g.Key, g => g.ToList());
            
            foreach (var content in taskContents)
            {
                var contentId = content.Id ?? string.Empty;
                var subs = submissionsByTask.TryGetValue(contentId, out var taskSubs) ? taskSubs : new List<Submission>();
                
                foreach (var s in students)
                {
                    if (!studentIdentifiers.ContainsKey(s.Id)) continue;
                    var identifiers = studentIdentifiers[s.Id];
                    
                    // Find ALL submissions for this student (match by StudentId, StudentEmail, or any identifier)
                    var studentSubmissions = subs
                        .Where(sub => 
                            identifiers.Contains(sub.StudentId) ||
                            (!string.IsNullOrEmpty(sub.StudentEmail) && identifiers.Contains(sub.StudentEmail, StringComparer.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrEmpty(sub.StudentName) && identifiers.Contains(sub.StudentName, StringComparer.OrdinalIgnoreCase)))
                        .OrderByDescending(sub => sub.UpdatedAt)
                        .ThenByDescending(sub => sub.SubmittedAt)
                        .ToList();

                    if (studentSubmissions.Count == 0) continue;

                    // Get the latest submission (prefer one with grade, but include all)
                    var latestForStudent = studentSubmissions.FirstOrDefault();
                    if (latestForStudent == null) continue;

                    // Check if this submission has a grade
                    var g = latestForStudent.Grade?.Trim() ?? string.Empty;
                    
                    // If no grade in latest, check other submissions for this student/task
                    if (string.IsNullOrWhiteSpace(g))
                    {
                        latestForStudent = studentSubmissions
                            .FirstOrDefault(sub => !string.IsNullOrWhiteSpace(sub.Grade?.Trim()));
                        if (latestForStudent == null) continue;
                        g = latestForStudent.Grade?.Trim() ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(g)) continue;

                    // Initialize gradeList for this student if needed
                    if (!gradeList.ContainsKey(s.Id)) gradeList[s.Id] = new List<string>();
                    if (!studentTaskGrades.ContainsKey(s.Id)) studentTaskGrades[s.Id] = new Dictionary<string, string>();

                    // Parse and format the grade
                    string formattedGrade;
                    if (g.Contains('/'))
                    {
                        formattedGrade = g;
                        var parts = g.Split('/');
                        if (parts.Length == 2 && double.TryParse(parts[0], out var got) && double.TryParse(parts[1], out var max) && max > 0)
                        {
                            var (a, tot) = taskTotals[s.Id];
                            a += got;
                            tot += max;
                            taskTotals[s.Id] = (a, tot);
                        }
                    }
                    else
                    {
                        var gClean = g.EndsWith("%") ? g.Substring(0, g.Length - 1) : g;
                        if (double.TryParse(gClean, out var numeric))
                        {
                            int mx;
                            if (!maxGradeCache.TryGetValue(latestForStudent.TaskId ?? string.Empty, out mx))
                            {
                                var taskContent = await _mongoDb.GetContentByIdAsync(latestForStudent.TaskId ?? string.Empty);
                                mx = (taskContent?.MaxGrade ?? 100);
                                if (mx <= 0) mx = 100;
                                maxGradeCache[latestForStudent.TaskId ?? string.Empty] = mx;
                            }
                            
                            var (a, tot) = taskTotals[s.Id];
                            if (g.EndsWith("%"))
                            {
                                var got = mx * (numeric / 100.0);
                                formattedGrade = $"{Math.Round(got, 2)}/{mx}";
                                a += got;
                                tot += mx;
                            }
                            else
                            {
                                formattedGrade = $"{numeric}/{mx}";
                                a += numeric;
                                tot += mx;
                            }
                            taskTotals[s.Id] = (a, tot);
                        }
                        else
                        {
                            formattedGrade = g; // Keep original if can't parse
                        }
                    }
                    
                    gradeList[s.Id].Add(formattedGrade);
                    // Store the grade for this specific task (using ContentItem.Id)
                    studentTaskGrades[s.Id][contentId] = formattedGrade;
                }
            }

            // Group attendance by date for multiple attendance columns
            var attendanceByDate = attendance
                .GroupBy(a => a.Date.Date)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(a => a.StudentId)
                          .ToDictionary(sg => sg.Key, sg => sg.OrderByDescending(x => x.Date).FirstOrDefault()?.Status ?? "Absent")
                );

            // Build student data with attendance snapshots
            var studentData = new List<object>();
            foreach (var s in students)
            {
                // Get enrollment student data to get Username (which is the student ID)
                EnrollmentStudent? enrollmentStudent = null;
                try
                {
                    if (!string.IsNullOrEmpty(s.StudentEmail))
                    {
                        enrollmentStudent = await _mongoDb.GetEnrollmentStudentByEmailAsync(s.StudentEmail);
                    }
                }
                catch { }
                if (enrollmentStudent == null)
                {
                    try
                    {
                        var subEmail = allSubmissions
                            .Where(ss => ss.StudentId == s.Id ||
                                         (!string.IsNullOrEmpty(s.StudentEmail) && string.Equals(ss.StudentEmail, s.StudentEmail, StringComparison.OrdinalIgnoreCase)))
                            .Select(ss => ss.StudentEmail)
                            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));
                        if (!string.IsNullOrEmpty(subEmail))
                        {
                            enrollmentStudent = await _mongoDb.GetEnrollmentStudentByEmailAsync(subEmail);
                        }
                    }
                    catch { }
                }

                // Prefer Enrollment _id; fallback to submission StudentId; then local Id
                string? subIdCandidate = null;
                try
                {
                    subIdCandidate = allSubmissions
                        .Where(ss => ss.StudentId == s.Id || (!string.IsNullOrEmpty(s.StudentEmail) && string.Equals(ss.StudentEmail, s.StudentEmail, StringComparison.OrdinalIgnoreCase)))
                        .Select(ss => ss.StudentId)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
                }
                catch { }
                var studentId = enrollmentStudent?.Id ?? enrollmentStudent?.Username ?? subIdCandidate ?? s.Id ?? string.Empty;
                
                // Get name fields from User collection
                string lastName = string.Empty;
                string firstName = string.Empty;
                string middleName = string.Empty;
                string fullName = string.Empty;
                
                if (!string.IsNullOrEmpty(s.StudentEmail))
                {
                    try
                    {
                        var user = await _mongoDb.GetUserByEmailAsync(s.StudentEmail);
                        if (user != null)
                        {
                            lastName = user.LastName ?? string.Empty;
                            firstName = user.FirstName ?? string.Empty;
                            middleName = user.MiddleName ?? string.Empty;
                            
                            // Build FullName from parts if available, otherwise use FullName field
                            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                            {
                                var nameParts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                                if (!string.IsNullOrWhiteSpace(middleName)) nameParts.Add(middleName);
                                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                                fullName = string.Join(" ", nameParts);
                            }
                            else if (!string.IsNullOrWhiteSpace(user.FullName))
                            {
                                fullName = user.FullName;
                            }
                        }
                    }
                    catch
                    {
                        // If lookup fails, continue with available data
                    }
                }
                
                // Fallback to StudentName if User fields are empty
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = s.StudentName ?? string.Empty;
                }

                var (attained, total) = taskTotals.TryGetValue(s.Id, out var tuple) ? tuple : (0, 0);

                // Get attendance statuses for each date (use both s.Id and studentId for lookup)
                var attendanceStatuses = new Dictionary<string, string>();
                foreach (var dateGroup in attendanceByDate)
                {
                    var dateLabel = $"Attendance_{dateGroup.Key:yyyy-MM-dd}";
                    // Try to find attendance by both Id and Username
                    var status = "Absent";
                    if (dateGroup.Value.TryGetValue(s.Id, out var st))
                    {
                        status = st;
                    }
                    else if (!string.IsNullOrEmpty(studentId) && dateGroup.Value.TryGetValue(studentId, out var st2))
                    {
                        status = st2;
                    }
                    attendanceStatuses[dateLabel] = status;
                }

                // Compute standing - use both s.Id and studentId for attendance lookup
                double attPct = 0;
                try
                {
                    var studentAtt = attendance.Where(a => a.StudentId == s.Id || a.StudentId == studentId).ToList();
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

                // Find latest submission for this student across all tasks
                var latestSubmission = allSubmissions
                    .Where(ss => ss.StudentId == s.Id || (!string.IsNullOrEmpty(s.StudentEmail) && string.Equals(ss.StudentEmail, s.StudentEmail, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(ss => ss.UpdatedAt)
                    .ThenByDescending(ss => ss.SubmittedAt)
                    .FirstOrDefault();

                string activityName = string.Empty;
                if (latestSubmission != null && !string.IsNullOrEmpty(latestSubmission.TaskId))
                {
                    try
                    {
                        var taskContent = await _mongoDb.GetContentByIdAsync(latestSubmission.TaskId);
                        activityName = taskContent?.Title ?? string.Empty;
                    }
                    catch { activityName = string.Empty; }
                }

                // Build task grades dictionary for this student
                var taskGradesDict = new Dictionary<string, string>();
                if (studentTaskGrades.TryGetValue(s.Id, out var stg))
                {
                    foreach (var kvp in stg)
                    {
                        taskGradesDict[kvp.Key] = kvp.Value;
                    }
                }

                studentData.Add(new
                {
                    studentId = studentId,
                    username = enrollmentStudent?.Username ?? studentId,
                    id = s.Id ?? string.Empty,
                    fullName = fullName,
                    email = s.StudentEmail ?? string.Empty,
                    attendanceStatuses = attendanceStatuses,
                    taskAttained = attained,
                    taskTotal = total,
                    taskGrades = gradeList.TryGetValue(s.Id, out var gl) ? string.Join(" | ", gl) : string.Empty,
                    taskGradesByTaskId = taskGradesDict,
                    submitted = latestSubmission?.Submitted ?? false,
                    fileName = latestSubmission?.FileName ?? string.Empty,
                    assessment = string.Empty,
                    standing = standing
                });
            }

            // Get unique attendance date labels
            var attendanceLabels = attendanceByDate.Keys
                .OrderBy(d => d)
                .Select(d => $"Attendance_{d:yyyy-MM-dd}")
                .ToList();

            // Build task information list from Content items (ordered by creation date)
            // Always return a list, even if empty
            // Include Title and Type from Content collection
            var taskInfo = taskContents != null && taskContents.Count > 0
                ? taskContents
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => (object)new
                    {
                        taskId = c.Id ?? string.Empty,
                        taskTitle = c.Title ?? string.Empty,
                        taskType = c.Type ?? string.Empty
                    })
                    .ToList()
                : new List<object>();

            return Ok(new
            {
                success = true,
                attendanceLabels = attendanceLabels ?? new List<string>(),
                tasks = taskInfo,
                students = studentData
            });
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
            
            // Get Content items where Type="task" instead of TaskItem
            // This ensures we get tasks with Title and Type from Content collection
            var allContentItemsCsv = await _mongoDb.GetContentsByClassIdAsync(classItem.Id);
            var taskContentsCsv = allContentItemsCsv.Where(c => c.Type?.ToLower() == "task").ToList();
            
            // Collect all submissions to ensure students with submissions are also included
            // Submissions reference ContentItem.Id (not TaskItem.Id)
            var allSubmissionsCsv = new List<Submission>();
            foreach (var content in taskContentsCsv)
            {
                var subs = await _mongoDb.GetTaskSubmissionsAsync(content.Id ?? string.Empty);
                allSubmissionsCsv.AddRange(subs);
            }

            // Ensure all students with submissions are included
            var studentsByEmailCsv = students.Where(s => !string.IsNullOrEmpty(s.StudentEmail))
                .ToDictionary(s => s.StudentEmail.Trim().ToLowerInvariant(), s => s, StringComparer.OrdinalIgnoreCase);
            var studentsByIdCsv = students.ToDictionary(s => s.Id, s => s);
            foreach (var sub in allSubmissionsCsv)
            {
                var emailKey = (sub.StudentEmail ?? string.Empty).Trim().ToLowerInvariant();
                var idKey = sub.StudentId ?? string.Empty;
                var exists = (!string.IsNullOrEmpty(emailKey) && studentsByEmailCsv.ContainsKey(emailKey)) ||
                             (!string.IsNullOrEmpty(idKey) && studentsByIdCsv.ContainsKey(idKey));
                if (!exists)
                {
                    var newStudent = new StudentPortal.Models.AdminDb.StudentRecord
                    {
                        Id = string.IsNullOrEmpty(idKey) ? Guid.NewGuid().ToString() : idKey,
                        ClassId = classItem.Id,
                        StudentName = sub.StudentName ?? string.Empty,
                        StudentEmail = sub.StudentEmail ?? string.Empty,
                        Status = "Active",
                        Grade = 0.0
                    };
                    students.Add(newStudent);
                    if (!string.IsNullOrEmpty(emailKey)) studentsByEmailCsv[emailKey] = newStudent;
                    if (!string.IsNullOrEmpty(newStudent.Id)) studentsByIdCsv[newStudent.Id] = newStudent;
                }
            }

            // Build per-student task totals (points attained / total points)
            var taskTotals = new Dictionary<string, (double attained, double total)>();
            var studentIdentifiers2 = new Dictionary<string, HashSet<string>>(); // Map StudentRecord.Id to all possible identifiers
            
            // Build comprehensive student identifier mapping for CSV export
            foreach (var s in students)
            {
                taskTotals[s.Id] = (0, 0);
                var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                identifiers.Add(s.Id);
                if (!string.IsNullOrEmpty(s.StudentEmail))
                {
                    identifiers.Add(s.StudentEmail);
                }
                studentIdentifiers2[s.Id] = identifiers;
            }
            
            // Also get enrollment student usernames for matching
            foreach (var s in students)
            {
                if (string.IsNullOrEmpty(s.StudentEmail)) continue;
                try
                {
                    var enrollmentStudent = await _mongoDb.GetEnrollmentStudentByEmailAsync(s.StudentEmail);
                    if (enrollmentStudent != null && !string.IsNullOrEmpty(enrollmentStudent.Username))
                    {
                        if (studentIdentifiers2.ContainsKey(s.Id))
                        {
                            studentIdentifiers2[s.Id].Add(enrollmentStudent.Username);
                        }
                    }
                }
                catch
                {
                    // Continue if enrollment lookup fails
                }
            }
            
            // Create a dictionary to store per-student per-task grades for CSV
            var studentTaskGradesCsv = new Dictionary<string, Dictionary<string, string>>();
            foreach (var s in students)
            {
                studentTaskGradesCsv[s.Id] = new Dictionary<string, string>();
            }
            
            var maxGradeCache2 = new Dictionary<string, int>();
            foreach (var content in taskContentsCsv)
            {
                var contentId = content.Id ?? string.Empty;
                var subs = await _mongoDb.GetTaskSubmissionsAsync(contentId);
                foreach (var sub in subs)
                {
                    // Find which student this submission belongs to by matching identifiers
                    string? matchedStudentId = null;
                    foreach (var kvp in studentIdentifiers2)
                    {
                        if (kvp.Value.Contains(sub.StudentId) || 
                            (!string.IsNullOrEmpty(sub.StudentEmail) && kvp.Value.Contains(sub.StudentEmail)))
                        {
                            matchedStudentId = kvp.Key;
                            break;
                        }
                    }
                    
                    if (matchedStudentId == null || !taskTotals.ContainsKey(matchedStudentId)) continue;
                    
                    var (a, tot) = taskTotals[matchedStudentId];
                    // Parse grade formats: "8/10", "90", or "90%"
                    var g = (sub.Grade ?? string.Empty).Trim();
                    if (!string.IsNullOrEmpty(g))
                    {
                        if (g.Contains('/'))
                        {
                            var parts = g.Split('/');
                            if (parts.Length == 2 && double.TryParse(parts[0], out var got) && double.TryParse(parts[1], out var max) && max > 0)
                            {
                                a += got;
                                tot += max;
                            }
                        }
                        else
                        {
                            var gClean = g.EndsWith("%") ? g.Substring(0, g.Length - 1) : g;
                            if (double.TryParse(gClean, out var numeric))
                            {
                                int mx;
                                if (!maxGradeCache2.TryGetValue(sub.TaskId ?? string.Empty, out mx))
                                {
                                    var taskContent = await _mongoDb.GetContentByIdAsync(sub.TaskId ?? string.Empty);
                                    mx = (taskContent?.MaxGrade ?? 100);
                                    if (mx <= 0) mx = 100;
                                    maxGradeCache2[sub.TaskId ?? string.Empty] = mx;
                                }
                                if (g.EndsWith("%"))
                                {
                                    var got = mx * (numeric / 100.0);
                                    a += got;
                                    tot += mx;
                                }
                                else
                                {
                                    a += numeric;
                                    tot += mx;
                                }
                            }
                        }
                    }
                    taskTotals[matchedStudentId] = (a, tot);
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

            // Order task contents by creation date for consistent column ordering
            var orderedTasks = taskContentsCsv.OrderBy(c => c.CreatedAt).ToList();

            // Build header with dynamic task columns
            var headerParts = new List<string> { "Student_ID", "Full_Name", attLabel };
            
            // Add dynamic task columns (NameOfTask and TaskGrade pairs)
            foreach (var content in orderedTasks)
            {
                headerParts.Add(content.Title ?? "NameOfTask");
                headerParts.Add("TaskGrade");
            }
            
            headerParts.Add("Assessment");
            headerParts.Add("Standing");
            
            var header = headerParts.ToArray();
            var lines = new List<string> { string.Join(",", header.Select(h => esc(h))) };

            foreach (var s in students)
            {
                // Get enrollment student data to get Username (which is the student ID)
                EnrollmentStudent? enrollmentStudent = null;
                try
                {
                    if (!string.IsNullOrEmpty(s.StudentEmail))
                    {
                        enrollmentStudent = await _mongoDb.GetEnrollmentStudentByEmailAsync(s.StudentEmail);
                    }
                }
                catch
                {
                    // If enrollment lookup fails, continue with available data
                }

                // Prefer Enrollment _id; fallback to submission StudentId; then local Id
                string? subIdCandidate = null;
                try
                {
                    subIdCandidate = allSubmissionsCsv
                        .Where(ss => ss.StudentId == s.Id || (!string.IsNullOrEmpty(s.StudentEmail) && string.Equals(ss.StudentEmail, s.StudentEmail, StringComparison.OrdinalIgnoreCase)))
                        .Select(ss => ss.StudentId)
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
                }
                catch { }
                var studentId = enrollmentStudent?.Id ?? enrollmentStudent?.Username ?? subIdCandidate ?? s.Id ?? string.Empty;

                // Get name fields from User collection
                string lastName = string.Empty;
                string firstName = string.Empty;
                string middleName = string.Empty;
                string fullName = string.Empty;
                
                if (!string.IsNullOrEmpty(s.StudentEmail))
                {
                    try
                    {
                        var user = await _mongoDb.GetUserByEmailAsync(s.StudentEmail);
                        if (user != null)
                        {
                            lastName = user.LastName ?? string.Empty;
                            firstName = user.FirstName ?? string.Empty;
                            middleName = user.MiddleName ?? string.Empty;
                            
                            // Build FullName from parts if available, otherwise use FullName field
                            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
                            {
                                var nameParts = new List<string>();
                                if (!string.IsNullOrWhiteSpace(firstName)) nameParts.Add(firstName);
                                if (!string.IsNullOrWhiteSpace(middleName)) nameParts.Add(middleName);
                                if (!string.IsNullOrWhiteSpace(lastName)) nameParts.Add(lastName);
                                fullName = string.Join(" ", nameParts);
                            }
                            else if (!string.IsNullOrWhiteSpace(user.FullName))
                            {
                                fullName = user.FullName;
                            }
                        }
                    }
                    catch
                    {
                        // If lookup fails, continue with available data
                    }
                }
                
                // Fallback to StudentName if User fields are empty
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = s.StudentName ?? string.Empty;
                }

                // Try to find attendance by both Id and Username
                var attStatus = "Absent";
                if (latestToday.TryGetValue(s.Id, out var st))
                {
                    attStatus = st;
                }
                else if (!string.IsNullOrEmpty(studentId) && latestToday.TryGetValue(studentId, out var st2))
                {
                    attStatus = st2;
                }
                else if (!string.IsNullOrWhiteSpace(s.Status))
                {
                    attStatus = s.Status;
                }

                var (attained, total) = taskTotals.TryGetValue(s.Id, out var tuple) ? tuple : (0, 0);
                var taskGrades = string.Empty;
                try
                {
                    if (!studentIdentifiers2.ContainsKey(s.Id)) 
                    {
                        taskGrades = string.Empty;
                    }
                    else
                    {
                        var identifiers = studentIdentifiers2[s.Id];
                        var gradeParts = new List<string>();
                        foreach (var content in taskContentsCsv)
                        {
                            var contentId = content.Id ?? string.Empty;
                            var subs = await _mongoDb.GetTaskSubmissionsAsync(contentId);
                            
                            // Find ALL submissions for this student (match by any identifier)
                            var studentSubmissions = subs
                                .Where(sub => 
                                    identifiers.Contains(sub.StudentId) ||
                                    (!string.IsNullOrEmpty(sub.StudentEmail) && identifiers.Contains(sub.StudentEmail, StringComparer.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrEmpty(sub.StudentName) && identifiers.Contains(sub.StudentName, StringComparer.OrdinalIgnoreCase)))
                                .OrderByDescending(sub => sub.UpdatedAt)
                                .ThenByDescending(sub => sub.SubmittedAt)
                                .ToList();

                            if (studentSubmissions.Count == 0) continue;

                            // Get the latest submission (prefer one with grade, but check all)
                            var latestForStudent = studentSubmissions.FirstOrDefault();
                            if (latestForStudent == null) continue;

                            // Check if this submission has a grade
                            var g = latestForStudent.Grade?.Trim() ?? string.Empty;
                            
                            // If no grade in latest, check other submissions for this student/task
                            if (string.IsNullOrWhiteSpace(g))
                            {
                                latestForStudent = studentSubmissions
                                    .FirstOrDefault(sub => !string.IsNullOrWhiteSpace(sub.Grade?.Trim()));
                                if (latestForStudent == null) continue;
                                g = latestForStudent.Grade?.Trim() ?? string.Empty;
                            }

                            if (string.IsNullOrWhiteSpace(g)) continue;
                            
                            int mx;
                            if (!maxGradeCache2.TryGetValue(latestForStudent.TaskId ?? string.Empty, out mx))
                            {
                                var taskContent = await _mongoDb.GetContentByIdAsync(latestForStudent.TaskId ?? string.Empty);
                                mx = (taskContent?.MaxGrade ?? 100);
                                if (mx <= 0) mx = 100;
                                maxGradeCache2[latestForStudent.TaskId ?? string.Empty] = mx;
                            }

                            string formatted;
                            if (g.Contains('/'))
                            {
                                formatted = g;
                            }
                            else
                            {
                                var gClean = g.EndsWith("%") ? g.Substring(0, g.Length - 1) : g;
                                if (double.TryParse(gClean, out var numeric))
                                {
                                    if (g.EndsWith("%"))
                                    {
                                        var got = mx * (numeric / 100.0);
                                        formatted = $"{Math.Round(got, 2)}/{mx}";
                                    }
                                    else
                                    {
                                        formatted = $"{numeric}/{mx}";
                                    }
                                }
                                else
                                {
                                    formatted = g; // Keep original if can't parse
                                }
                            }

                            gradeParts.Add(formatted);
                            // Store the grade for this specific task (using ContentItem.Id)
                            if (studentTaskGradesCsv.ContainsKey(s.Id))
                            {
                                studentTaskGradesCsv[s.Id][contentId] = formatted;
                            }
                        }
                        taskGrades = string.Join("; ", gradeParts);
                    }
                }
                catch 
                {
                    taskGrades = string.Empty;
                }

                // Assessment left blank for manual input
                var assessmentDisplay = string.Empty;

                // Compute Standing using simple weights: Attendance 20%, Task 50%, Assessment 30%
                // Attendance percent from all records: Present=1, Late=0.5, Absent=0
                double attPct = 0;
                try
                {
                    // Use both s.Id and studentId for attendance lookup
                    var studentAtt = attendance.Where(a => a.StudentId == s.Id || a.StudentId == studentId).ToList();
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

                // Latest submission for submitted/file name
                Submission? latestSubmissionForRow = null;
                try
                {
                    latestSubmissionForRow = allSubmissionsCsv
                        .Where(ss => ss.StudentId == s.Id || (!string.IsNullOrEmpty(s.StudentEmail) && string.Equals(ss.StudentEmail, s.StudentEmail, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(ss => ss.UpdatedAt)
                        .ThenByDescending(ss => ss.SubmittedAt)
                        .FirstOrDefault();
                }
                catch { }
                var submittedStr = (latestSubmissionForRow?.Submitted ?? false) ? "Yes" : "No";
                var fileNameStr = latestSubmissionForRow?.FileName ?? string.Empty;

                // Build row with dynamic task columns
                var rowParts = new List<string>
                {
                    esc(studentId), // Use Username as student ID
                    esc(fullName), // Use FullName instead of splitting
                    esc(attStatus)
                };
                
                // Add task data for each task (in the same order as header)
                // Each task gets two columns: NameOfTask (shows task title) and TaskGrade (shows grade)
                var taskGradesByTaskId = studentTaskGradesCsv.TryGetValue(s.Id, out var stg) ? stg : new Dictionary<string, string>();
                foreach (var content in orderedTasks)
                {
                    var contentId = content.Id ?? string.Empty;
                    rowParts.Add(esc(content.Title ?? string.Empty)); // NameOfTask column - shows task title from Content
                    rowParts.Add(esc(taskGradesByTaskId.TryGetValue(contentId, out var grade) ? grade : string.Empty)); // TaskGrade column - shows grade
                }
                
                rowParts.Add(esc(assessmentDisplay));
                rowParts.Add(esc(standing));
                
                lines.Add(string.Join(",", rowParts));
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

        public class UnenrollRequest
        {
            public string StudentId { get; set; }
            public string ClassCode { get; set; }
            public string StudentEmail { get; set; }
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

        [HttpPost("Unenroll")]
        public async Task<IActionResult> Unenroll([FromBody] UnenrollRequest req)
        {
            if (req == null)
                return BadRequest(new { success = false, message = "Invalid request." });
            if (string.IsNullOrWhiteSpace(req.ClassCode))
                return BadRequest(new { success = false, message = "ClassCode is required." });

            try
            {
                bool removed = false;

                if (!string.IsNullOrWhiteSpace(req.StudentId))
                {
                    var classItem = await _mongoDb.GetClassByCodeAsync(req.ClassCode);
                    if (classItem == null)
                        return NotFound(new { success = false, message = $"Class with code {req.ClassCode} not found." });

                    removed = await _mongoDb.RemoveStudentFromClassById(req.StudentId, classItem.Id);
                }
                else if (!string.IsNullOrWhiteSpace(req.StudentEmail))
                {
                    removed = await _mongoDb.RemoveStudentFromClassByEmail(req.StudentEmail, req.ClassCode);
                }
                else
                {
                    return BadRequest(new { success = false, message = "StudentId or StudentEmail is required." });
                }

                if (!removed)
                    return NotFound(new { success = false, message = "Student not enrolled or already removed." });

                return Ok(new { success = true, message = "Student has been un-enrolled from the class." });
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
                return StatusCode(500, new { success = false, message = $"Failed to un-enroll student: {ex.Message}" });
            }
        }
    }
}
