using Microsoft.AspNetCore.Mvc;
using StudentPortal.Models.StudentDb;
using StudentPortal.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    [Route("studentdb/[controller]")]
    public class StudentTodoController : Controller
    {
        private readonly MongoDbService _mongoDb;

        public StudentTodoController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet("")]
        [HttpGet("/StudentDb/StudentTodo")]
        [HttpGet("/StudentTodo")]
        public async Task<IActionResult> Index()
        {
            var email = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(email))
            {
                var emptyVm = new StudentTodoViewModel
                {
                    StudentName = "Student",
                    StudentInitials = "ST",
                    Subjects = new List<SubjectTodo>()
                };
                return View("~/Views/StudentDb/StudentTodo/Index.cshtml", emptyVm);
            }

            var user = await _mongoDb.GetUserByEmailAsync(email);
            if (user == null)
            {
                var emptyVm = new StudentTodoViewModel
                {
                    StudentName = "Student",
                    StudentInitials = "ST",
                    Subjects = new List<SubjectTodo>()
                };
                return View("~/Views/StudentDb/StudentTodo/Index.cshtml", emptyVm);
            }

            var classCodes = user.JoinedClasses ?? new List<string>();
            var classes = classCodes.Count > 0 ? await _mongoDb.GetClassesByCodesAsync(classCodes) : new List<StudentPortal.Models.AdminDb.ClassItem>();

            var subjects = new Dictionary<string, SubjectTodo>(StringComparer.OrdinalIgnoreCase);

            foreach (var cls in classes)
            {
                var subjectName = string.IsNullOrWhiteSpace(cls.SubjectName) ? "Subject" : cls.SubjectName;
                if (!subjects.ContainsKey(subjectName))
                    subjects[subjectName] = new SubjectTodo { Title = subjectName, Tasks = new List<TaskItem>() };

                var contents = await _mongoDb.GetContentsByClassIdAsync(cls.Id);
                var tasks = contents.Where(c => string.Equals(c.Type, "task", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var t in tasks)
                {
                    var submitted = false;
                    if (!string.IsNullOrEmpty(user.Id) && !string.IsNullOrEmpty(t.Id))
                    {
                        var sub = await _mongoDb.GetSubmissionByStudentAndTaskAsync(user.Id, t.Id);
                        submitted = sub?.Submitted == true;
                    }

                    if (submitted)
                        continue;

                    var deadline = t.Deadline;
                    var now = DateTime.UtcNow;
                    var status = deadline.HasValue && deadline.Value < now ? "pastdue" : "todo";
                    var color = "yellow";
                    if (status == "pastdue")
                    {
                        color = "red";
                    }
                    else if (deadline.HasValue)
                    {
                        var days = (deadline.Value.Date - now.Date).TotalDays;
                        color = days >= 7 ? "green" : days >= 3 ? "yellow" : "red";
                    }
                    else
                    {
                        color = "green";
                    }

                    subjects[subjectName].Tasks.Add(new TaskItem
                    {
                        Name = string.IsNullOrWhiteSpace(t.Title) ? "Task" : t.Title,
                        Deadline = deadline.HasValue ? deadline.Value.ToString("MMM d, yyyy") : "No deadline",
                        Status = status,
                        ColorClass = color
                    });
                }
            }

            var vm = new StudentTodoViewModel
            {
                StudentName = string.IsNullOrWhiteSpace(user.FullName) ? "Student" : user.FullName,
                StudentInitials = GetInitials(user.FullName),
                Subjects = subjects.Values.OrderBy(s => s.Title).ToList()
            };

            return View("~/Views/StudentDb/StudentTodo/Index.cshtml", vm);
        }

        private string GetInitials(string name)
        {
            var parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return ($"{parts[0][0]}{parts[^1][0]}").ToUpper();
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return "ST";
        }
    }
}
