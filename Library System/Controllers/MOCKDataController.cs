using Microsoft.AspNetCore.Mvc;
using SystemLibrary.Services;
using SystemLibrary.Models;
using SystemLibrary.Data;
using MongoDB.Driver;

namespace SystemLibrary.Controllers
{
    public class MOCKDataController : Controller
    {
        private readonly IMOCKDataService _MOCKDataService;

        public MOCKDataController(IMOCKDataService MOCKDataService)
        {
            _MOCKDataService = MOCKDataService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                ViewBag.Role = "Admin";
                
                var students = await _MOCKDataService.GetAllStudentsAsync();
                var staff = await _MOCKDataService.GetAllStaffAsync();
                
                // Debug information
                ViewBag.DebugInfo = $"Students: {students?.Count ?? 0}, Staff: {staff?.Count ?? 0}";
                
                ViewBag.Students = students ?? new List<StudentMOCKData>();
                ViewBag.Staff = staff ?? new List<StaffMOCKData>();
                
                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Role = "Admin";
                ViewBag.Students = new List<StudentMOCKData>();
                ViewBag.Staff = new List<StaffMOCKData>();
                ViewBag.Error = $"Error loading MOCK data: {ex.Message}";
                ViewBag.DebugInfo = $"Exception: {ex.Message}";
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> SeedData()
        {
            try
            {
                // Clear existing data first
                await _MOCKDataService.ClearAllMACDataAsync();
                
                var seeder = new MOCKDataSeeder(_MOCKDataService);
                await seeder.SeedSampleDataAsync();
                
                TempData["SuccessMessage"] = "MOCK data seeded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error seeding MOCK data: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> TestLookup()
        {
            try
            {
                // Test student lookup
                var student = await _MOCKDataService.GetStudentByNumberAsync("2024-0001");
                var staff = await _MOCKDataService.GetStaffByEmployeeIdAsync("EMP-001");
                
                return Json(new { 
                    success = true, 
                    studentFound = student != null,
                    staffFound = staff != null,
                    studentName = student?.FullName,
                    staffName = staff?.FullName
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ForceSeed()
        {
            try
            {
                // Direct database seeding - bypass service layer
                var database = _MOCKDataService.GetDatabase();
                var studentCollection = database.GetCollection<StudentMOCKData>("StudentMOCKData");
                var staffCollection = database.GetCollection<StaffMOCKData>("StaffMOCKData");

                // Clear existing data
                await studentCollection.DeleteManyAsync(Builders<StudentMOCKData>.Filter.Empty);
                await staffCollection.DeleteManyAsync(Builders<StaffMOCKData>.Filter.Empty);

                // Insert sample students
                var students = new List<StudentMOCKData>
                {
                    new StudentMOCKData
                    {
                        StudentNumber = "2024-0001",
                        FullName = "Juan Carlos Dela Cruz",
                        Course = "BSIT",
                        YearLevel = "3rd Year",
                        Program = "Bachelor of Science in Information Technology",
                        Department = "College of Computer Studies",
                        ContactNumber = "09123456789",
                        Email = "juan.delacruz@student.mac.edu.ph",
                        IsEnrolled = true,
                        EnrollmentDate = new DateTime(2024, 6, 15),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new StudentMOCKData
                    {
                        StudentNumber = "2024-0002",
                        FullName = "Maria Santos Rodriguez",
                        Course = "BSCS",
                        YearLevel = "2nd Year",
                        Program = "Bachelor of Science in Computer Science",
                        Department = "College of Computer Studies",
                        ContactNumber = "09123456790",
                        Email = "maria.rodriguez@student.mac.edu.ph",
                        IsEnrolled = true,
                        EnrollmentDate = new DateTime(2024, 6, 15),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                // Insert sample staff
                var staff = new List<StaffMOCKData>
                {
                    new StaffMOCKData
                    {
                        EmployeeId = "EMP-001",
                        FullName = "Dr. Sarah Johnson",
                        Department = "College of Computer Studies",
                        Position = "Dean",
                        Email = "sarah.johnson@mac.edu.ph",
                        ContactNumber = "09123456795",
                        EmploymentType = "Full-time",
                        HireDate = new DateTime(2020, 1, 15),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new StaffMOCKData
                    {
                        EmployeeId = "EMP-002",
                        FullName = "Ms. Jennifer Lee",
                        Department = "Library Services",
                        Position = "Head Librarian",
                        Email = "jennifer.lee@mac.edu.ph",
                        ContactNumber = "09123456797",
                        EmploymentType = "Full-time",
                        HireDate = new DateTime(2021, 3, 1),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await studentCollection.InsertManyAsync(students);
                await staffCollection.InsertManyAsync(staff);

                TempData["SuccessMessage"] = "MOCK data force seeded successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error force seeding MOCK data: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }
    }
}
