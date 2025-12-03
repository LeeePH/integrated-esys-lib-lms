using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using ProfessorAccountCreation.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ProfessorAccountCreation.Controllers
{
    public class SuperAdminController : Controller
    {
        private readonly MongoDbContext _context;

        public SuperAdminController(MongoDbContext context)
        {
            _context = context;
        }

        // ======= LOGIN =======
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var admin = _context.ProfAdmins.Find(a => a.Email == email).FirstOrDefault();
            if (admin == null)
            {
                ViewBag.Error = "Wrong Credentials, please try again";
                return View();
            }

            if (!VerifyPassword(password, admin.PasswordHash))
            {
                ViewBag.Error = "Wrong Credentials, please try again";
                return View();
            }

            // Generate OTP if expired or null
            if (string.IsNullOrEmpty(admin.OTP) || admin.OTPExpiresAt < DateTime.UtcNow)
            {
                admin.OTP = GenerateOTP();
                admin.OTPExpiresAt = DateTime.UtcNow.AddHours(6);
                var update = Builders<ProfAdmin>.Update
                    .Set(a => a.OTP, admin.OTP)
                    .Set(a => a.OTPExpiresAt, admin.OTPExpiresAt);
                _context.ProfAdmins.UpdateOne(a => a.Id == admin.Id, update);

                // Send OTP via email
                EmailHelper.SendOTP(admin.Email, admin.OTP);
            }

            TempData["ProfAdminId"] = admin.Id;
            return RedirectToAction("VerifyOTP");
        }

        public IActionResult VerifyOTP()
        {
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOTP(string otp)
        {
            var adminId = TempData["ProfAdminId"] as string;
            if (adminId == null) return RedirectToAction("Login");

            var admin = _context.ProfAdmins.Find(a => a.Id == adminId).FirstOrDefault();
            if (admin == null)
                return RedirectToAction("Login");

            if (admin.OTP != otp || admin.OTPExpiresAt < DateTime.UtcNow)
            {
                ViewBag.Error = "Invalid or expired OTP";
                return View();
            }

            // OTP verified, store session
            HttpContext.Session.SetString("ProfAdminId", admin.Id);

            return RedirectToAction("Index"); // dashboard
        }

        // ======= LOGOUT =======
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("ProfAdminId");
            return RedirectToAction("Login");
        }

        private string GenerateOTP()
        {
            using var rng = new RNGCryptoServiceProvider();
            var bytes = new byte[6];
            rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 6);
        }

        private bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password))
                return false;

            // New seeded format: "{iterations}.{saltBase64}.{hashBase64}"
            if (hash.Contains('.'))
            {
                var parts = hash.Split('.');
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out var iterations)) return false;

                byte[] salt;
                byte[] storedHash;
                try
                {
                    salt = Convert.FromBase64String(parts[1]);
                    storedHash = Convert.FromBase64String(parts[2]);
                }
                catch
                {
                    return false;
                }

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(storedHash.Length);

                // Constant-time comparison
                return CryptographicOperations.FixedTimeEquals(computed, storedHash);
            }

            // Legacy format: SHA256 hex string
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hashBytes = sha.ComputeHash(bytes);
            var computedHex = BitConverter.ToString(hashBytes).Replace("-", "");
            return string.Equals(computedHex, hash, StringComparison.OrdinalIgnoreCase);
        }

        // ======= DASHBOARD / CRUD =======
        public IActionResult Index(string department, string search, int page = 1, int pageSize = 10)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("ProfAdminId")))
                return RedirectToAction("Login");

            var filter = Builders<Professor>.Filter.Empty;

            // Filter by department
            if (!string.IsNullOrEmpty(department) && department != "All")
            {
                filter &= Builders<Professor>.Filter.AnyEq(p => p.Programs, department);
            }

            // Filter by search
            if (!string.IsNullOrEmpty(search))
            {
                filter &= Builders<Professor>.Filter.Or(
                    Builders<Professor>.Filter.Regex(p => p.LastName, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    Builders<Professor>.Filter.Regex(p => p.GivenName, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                    Builders<Professor>.Filter.Regex(p => p.Email, new MongoDB.Bson.BsonRegularExpression(search, "i"))
                );
            }

            var totalProfessors = _context.Professors.CountDocuments(filter);
            var totalPages = (int)Math.Ceiling((double)totalProfessors / pageSize);

            var professors = _context.Professors
                .Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToList()
                .OrderBy(p => p.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.GivenName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ViewBag.SelectedDepartment = department ?? "All";
            ViewBag.Search = search ?? "";
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            return View(professors);
        }

        [HttpPost]
        [HttpPost]
        public IActionResult Delete(string id)
        {
            try
            {
                _context.ProfessorAssignments.DeleteOne(a => a.ProfessorId == id);
                _context.Professors.DeleteOne(p => p.Id == id);
            }
            catch
            {
                // Log error if needed
            }

            return RedirectToAction("Index");
        }


        public IActionResult Edit(string id)
        {
            var professor = _context.Professors.Find(p => p.Id == id).FirstOrDefault();
            if (professor == null) return NotFound();
            return View(professor);
        }

        [HttpPost]
    
        public IActionResult Edit(Professor updatedProfessor, string BachelorSelect, string MastersSelect, string PhDSelect, string LicensesSelect, string FacultyRole)
        {
            if (!ModelState.IsValid) return View(updatedProfessor);

            // Handle dropdown + "Other" logic
            updatedProfessor.Bachelor = BachelorSelect == "OTHER" ? updatedProfessor.Bachelor : BachelorSelect;
            updatedProfessor.Masters = MastersSelect == "OTHER" ? updatedProfessor.Masters : MastersSelect;
            updatedProfessor.PhD = PhDSelect == "OTHER" ? updatedProfessor.PhD : PhDSelect;
            updatedProfessor.Licenses = LicensesSelect == "OTHER" ? updatedProfessor.Licenses : LicensesSelect;

            updatedProfessor.FacultyRole = FacultyRole; // <-- Save faculty role

            var update = Builders<Professor>.Update
                .Set(p => p.LastName, updatedProfessor.LastName)
                .Set(p => p.GivenName, updatedProfessor.GivenName)
                .Set(p => p.MiddleName, updatedProfessor.MiddleName)
                .Set(p => p.Extension, updatedProfessor.Extension)
                .Set(p => p.Email, updatedProfessor.Email)
                .Set(p => p.Programs, updatedProfessor.Programs)
                .Set(p => p.Bachelor, updatedProfessor.Bachelor)
                .Set(p => p.Masters, updatedProfessor.Masters)
                .Set(p => p.PhD, updatedProfessor.PhD)
                .Set(p => p.Licenses, updatedProfessor.Licenses)
                .Set(p => p.FacultyRole, updatedProfessor.FacultyRole); // <--- needed


            _context.Professors.UpdateOne(p => p.Id == updatedProfessor.Id, update);
            return RedirectToAction("EditSuccess");
        }

        public IActionResult EditSuccess()
        {
            return View();
        }


    }
}
