using Microsoft.AspNetCore.Mvc;
using StudentPortal.Services;
using StudentPortal.Models.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StudentPortal.Controllers
{
    public class LibraryController : Controller
    {
        private readonly LibraryService _libraryService;
        private readonly MongoDbService _mongoService;

        public LibraryController(LibraryService libraryService, MongoDbService mongoService)
        {
            _libraryService = libraryService;
            _mongoService = mongoService;
        }

        [HttpGet]   
        public async Task<IActionResult> Search()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            // Get available books by default
            var books = await _libraryService.GetAvailableBooksAsync();
            
            ViewBag.SearchTerm = "";
            ViewBag.UserEmail = userEmail;
            
            return View(books);
        }

        [HttpPost]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UserEmail = userEmail;
            ViewBag.SearchTerm = searchTerm ?? "";

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                var availableBooks = await _libraryService.GetAvailableBooksAsync();
                return View(availableBooks);
            }

            var books = await _libraryService.SearchBooksAsync(searchTerm);
            return View(books);
        }

        // Lightweight JSON search API for in-page library modals (StudentTask/StudentMaterial)
        [HttpGet]
        public async Task<IActionResult> ApiSearch(string q)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return Json(new { success = false, message = "Please log in as a student." });
            }

            try
            {
                var term = q ?? string.Empty;
                Console.WriteLine($"[LibraryController.ApiSearch] Searching for: '{term}'");
                
                List<Book> books;
                
                if (string.IsNullOrWhiteSpace(term))
                {
                    // When no search term, get ALL books from database (no filters - includes unavailable books)
                    Console.WriteLine($"[LibraryController.ApiSearch] No search term - getting ALL books (including unavailable)...");
                    books = await _libraryService.GetAllBooksAsync();
                }
                else
                {
                    // When there's a search term, search for ALL matching books (no availability filters)
                    Console.WriteLine($"[LibraryController.ApiSearch] Search term provided - searching ALL books (including unavailable)...");
                    books = await _libraryService.SearchBooksAsync(term);
                }
                
                Console.WriteLine($"[LibraryController.ApiSearch] Found {books?.Count ?? 0} total books (all statuses)");

                if (books == null || books.Count == 0)
                {
                    Console.WriteLine($"[LibraryController.ApiSearch] WARNING: No books found in database");
                    return Json(new { success = true, books = new List<object>() });
                }

                // Include ALL books in result - don't filter by availability
                var result = books.Select(b => new
                {
                    id = b._id.ToString(),
                    title = b.Title ?? "",
                    author = b.Author ?? "",
                    subject = b.Subject ?? "",
                    category = b.Subject ?? "",
                    description = $"{b.Publisher}".Trim(),
                    isAvailable = b.IsAvailable, // This is just for display - we still include unavailable books
                    availableCopies = b.AvailableCopies,
                    totalCopies = b.TotalCopies,
                    isReferenceOnly = b.IsReferenceOnly
                }).ToList();

                var availableCount = result.Count(b => b.isAvailable);
                var unavailableCount = result.Count - availableCount;
                Console.WriteLine($"[LibraryController.ApiSearch] Returning {result.Count} books ({availableCount} available, {unavailableCount} unavailable)");
                return Json(new { success = true, books = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryController.ApiSearch] Error: {ex.Message}");
                Console.WriteLine($"[LibraryController.ApiSearch] Stack trace: {ex.StackTrace}");
                return Json(new { success = false, message = $"Error searching library: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ReserveBook(string bookId)
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return Json(new { success = false, message = "Please log in as a student to reserve books." });
            }

            if (string.IsNullOrEmpty(bookId))
            {
                return Json(new { success = false, message = "Book ID is required." });
            }

            try
            {
                // Get student number from library system
                var studentNumber = await _libraryService.GetStudentNumberAsync(userEmail);
                
                // If not found in library system, try to get from enrollment system
                if (string.IsNullOrEmpty(studentNumber))
                {
                    var enrollmentStudent = await _mongoService.GetEnrollmentStudentByEmailAsync(userEmail);
                    if (enrollmentStudent != null)
                    {
                        // EnrollmentStudent doesn't have StudentNumber, use Username or Email
                        studentNumber = !string.IsNullOrEmpty(enrollmentStudent.Username) 
                            ? enrollmentStudent.Username 
                            : enrollmentStudent.Email;
                    }
                    else
                    {
                        studentNumber = userEmail; // Fallback to email
                    }
                }

                // Get user ID from library system (try to find by email)
                var libraryUserId = await _libraryService.GetLibraryUserIdAsync(userEmail);
                
                // Use library user ID if found, otherwise use email as identifier
                var userIdForReservation = !string.IsNullOrEmpty(libraryUserId) ? libraryUserId : userEmail;
                
                var result = await _libraryService.CreateReservationAsync(userIdForReservation, bookId, studentNumber);

                if (result.Success)
                {
                    return Json(new { success = true, message = result.Message });
                }
                else
                {
                    return Json(new { success = false, message = result.Message });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibraryController] Error reserving book: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Debug endpoint to test database connection and count books
        [HttpGet]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var allBooks = await _libraryService.GetAllBooksAsync();
                var availableBooks = await _libraryService.GetAvailableBooksAsync();
                
                // Get diagnostic info
                var diagnostics = await _libraryService.GetDiagnosticsAsync();
                
                return Json(new 
                { 
                    success = true, 
                    totalBooks = allBooks?.Count ?? 0,
                    availableBooks = availableBooks?.Count ?? 0,
                    message = $"Found {allBooks?.Count ?? 0} total books, {availableBooks?.Count ?? 0} available",
                    diagnostics = diagnostics
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}", stackTrace = ex.StackTrace });
            }
        }

        [HttpGet]
        public async Task<IActionResult> MyReservations()
        {
            var userEmail = HttpContext.Session.GetString("UserEmail");
            var userId = HttpContext.Session.GetString("UserId");
            var userRole = HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrEmpty(userEmail) || userRole?.ToLower() != "student")
            {
                return RedirectToAction("Login", "Account");
            }

            var libraryUserId = userId ?? userEmail;
            var reservations = await _libraryService.GetUserReservationsAsync(libraryUserId);
            
            return View(reservations);
        }
    }
}

    