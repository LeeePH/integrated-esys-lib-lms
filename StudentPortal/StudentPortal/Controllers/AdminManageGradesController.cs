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
	public class AdminManageGradesController : Controller
	{
        private readonly MongoDbService _mongoDb;

        public AdminManageGradesController(MongoDbService mongoDb)
        {
            _mongoDb = mongoDb;
        }

        [HttpGet]
		public IActionResult Index()
		{
			var viewModel = new AdminManageGradesViewModel
			{
				AdminName = "Admin",
				AdminInitials = "AD",
				Students = new List<StudentGradeRecord>
				{
					new StudentGradeRecord
					{
						Id = "s001",
						Last = "Aguirre",
						First = "Maria",
						Middle = "S.",
						AvatarColor = "#6b89d4",
						Tasks = new List<GradeTask>
						{
							new GradeTask { Id = "t1", Name = "Homework 1", Submitted = true, Score = 9.5, Max = 10, Due = new DateTime(2025,10,5) },
							new GradeTask { Id = "t2", Name = "Project Outline", Submitted = false, Score = null, Max = 15, Due = new DateTime(2025,10,12) }
						},
						Assessments = new List<GradeAssessment>
						{
							new GradeAssessment { Id = "a1", Name = "Quiz 1", Submitted = true, Score = 18, Max = 20, Due = new DateTime(2025,10,10) }
						},
						Attendance = new List<GradeAttendance>
						{
							new GradeAttendance { Date = new DateTime(2025,10,1), Status = "Present" },
							new GradeAttendance { Date = new DateTime(2025,10,8), Status = "Absent" },
							new GradeAttendance { Date = new DateTime(2025,10,15), Status = "Late" }
						}
					},

					new StudentGradeRecord
					{
						Id = "s002",
						Last = "Bautista",
						First = "Juan",
						Middle = "R.",
						AvatarColor = "#22c55e",
						Tasks = new List<GradeTask>
						{
							new GradeTask { Id = "t3", Name = "Worksheet", Submitted = true, Score = 8, Max = 10, Due = new DateTime(2025,10,7) }
						},
						Assessments = new List<GradeAssessment>
						{
							new GradeAssessment { Id = "a2", Name = "Midterm", Submitted = false, Score = null, Max = 50, Due = new DateTime(2025,10,14) }
						},
						Attendance = new List<GradeAttendance>
						{
							new GradeAttendance { Date = new DateTime(2025,10,1), Status = "Present" },
							new GradeAttendance { Date = new DateTime(2025,10,8), Status = "Present" }
						}
					},

					new StudentGradeRecord
					{
						Id = "s003",
						Last = "Cruz",
						First = "Anna",
						Middle = "L.",
						AvatarColor = "#f59e0b",
						Tasks = new List<GradeTask>(),
						Assessments = new List<GradeAssessment>(),
						Attendance = new List<GradeAttendance>
						{
							new GradeAttendance { Date = new DateTime(2025,10,1), Status = "Absent" },
							new GradeAttendance { Date = new DateTime(2025,10,8), Status = "Absent" }
						}
					}
				}
			};

			return View("~/Views/AdminDb/AdminManageGrades/Index.cshtml", viewModel);
		}

		// -------------------------------------------
		// IMPORT GRADES ENDPOINT -> AttendanceCopy collection
		// -------------------------------------------
		[HttpPost]
		public async Task<IActionResult> ImportGrades([FromBody] ImportGradesRequest req)
		{
			if (req?.Rows == null || req.Rows.Count == 0)
				return BadRequest(new { success = false, message = "No rows received" });

			// Insert incoming rows into MongoDB "AttendanceCopy" collection.
			// Mongo will auto-create the collection on first insert.
			try
			{
				await _mongoDb.InsertAttendanceCopyRowsAsync(req.Rows);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ImportGrades] Failed to insert into AttendanceCopy: {ex.Message}");
				return StatusCode(500, new { success = false, message = "Failed to save to AttendanceCopy" });
			}

			Console.WriteLine($"[ImportGrades] Inserted {req.Rows.Count} rows into AttendanceCopy");
			return Ok(new { success = true });
		}
	}

	public class ImportGradesRequest
	{
		public List<Dictionary<string, string>> Rows { get; set; } = new();
	}
}
