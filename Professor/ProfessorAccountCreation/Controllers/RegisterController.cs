using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ProfessorAccountCreation.Models;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

namespace ProfessorAccountCreation.Controllers
{
    public class RegisterController : Controller
    {
        private readonly MongoDbContext _context;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public RegisterController(MongoDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> Register(Professor professor)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Some fields contain invalid inputs. Please check the highlighted fields and try again.";
                return View("Index", professor);
            }

            // ---------- NEW: READ FACULTY ROLE  ----------
            professor.FacultyRole = Request.Form["FacultyRole"];
            if (string.IsNullOrWhiteSpace(professor.FacultyRole))
            {
                ViewBag.Error = "Faculty Role is required.";
                return View("Index", professor);
            }

            // ---------- NEW: EDUCATION: DROPDOWNS + OTHER ----------
            string bachelorSelect = Request.Form["BachelorSelect"];
            string bachelorOther = Request.Form["Bachelor"];
            professor.Bachelor = bachelorSelect == "OTHER" ? bachelorOther : bachelorSelect;

            string mastersSelect = Request.Form["MastersSelect"];
            string mastersOther = Request.Form["Masters"];
            professor.Masters = mastersSelect == "OTHER" ? mastersOther : mastersSelect;

            string phdSelect = Request.Form["PhDSelect"];
            string phdOther = Request.Form["PhD"];
            professor.PhD = phdSelect == "OTHER" ? phdOther : phdSelect;

            string licenseSelect = Request.Form["LicensesSelect"];
            string licenseOther = Request.Form["Licenses"];
            professor.Licenses = licenseSelect == "OTHER" ? licenseOther : licenseSelect;

            // 1) Duplicate check
            var nameFilter = Builders<Professor>.Filter.And(
                Builders<Professor>.Filter.Regex(p => p.GivenName, new MongoDB.Bson.BsonRegularExpression($"^{RegexEscape(professor.GivenName)}$", "i")),
                Builders<Professor>.Filter.Regex(p => p.LastName, new MongoDB.Bson.BsonRegularExpression($"^{RegexEscape(professor.LastName)}$", "i")),
                Builders<Professor>.Filter.Or(
                    Builders<Professor>.Filter.Regex(p => p.MiddleName, new MongoDB.Bson.BsonRegularExpression($"^{RegexEscape(professor.MiddleName ?? "")}$", "i")),
                    Builders<Professor>.Filter.Eq(p => p.MiddleName, null)
                ),
                Builders<Professor>.Filter.Or(
                    Builders<Professor>.Filter.Regex(p => p.Extension, new MongoDB.Bson.BsonRegularExpression($"^{RegexEscape(professor.Extension ?? "")}$", "i")),
                    Builders<Professor>.Filter.Eq(p => p.Extension, null)
                )
            );

            var emailFilter = Builders<Professor>.Filter.Regex(p => p.Email, new MongoDB.Bson.BsonRegularExpression($"^{RegexEscape(professor.Email)}$", "i"));

            var finalFilter = Builders<Professor>.Filter.Or(nameFilter, emailFilter);
            var existingProfessor = _context.Professors.Find(finalFilter).FirstOrDefault();

            if (existingProfessor != null)
            {
                ViewBag.Error = "A professor with the same name or email already exists.";
                return View("Index", professor);
            }

            // 2) Validate email using Abstract API
            bool isEmailValid;
            try
            {
                isEmailValid = await ValidateEmailAsync(professor.Email);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Failed to validate email. Please try again later.";
                // log ex
                return View("Index", professor);
            }

            if (!isEmailValid)
            {
                ViewBag.Error = "The email address provided is invalid or does not exist.";
                return View("Index", professor);
            }

            // 3) Generate temporary password
            var tempPassword = GenerateTemporaryPassword(12);

            // 4) Hash it
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

            professor.PasswordHash = passwordHash;
            professor.IsTemporaryPassword = true;
            professor.TempPasswordExpiresAt = DateTime.UtcNow.AddDays(7);

            // 5) Insert into DB
            try
            {
                _context.Professors.InsertOne(professor);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred saving to the database. Please try again.";
                return View("Index", professor);
            }

            // 5B) Also create blank professor assignment document aligned with new model
            try
            {
                var assignment = new ProfessorAssignment
                {
                    ProfessorId = professor.Id!,
                    ClassMeetingIds = new List<string>() // empty initially, can add up to 7 later
                };

                _context.ProfessorAssignments.InsertOne(assignment);

            }
            catch (Exception ex)
            {
                // Rollback: remove professor if assignment fails
                _context.Professors.DeleteOne(p => p.Id == professor.Id);

                ViewBag.Error = "An error occurred creating the professor assignment. Please try again.";
                return View("Index", professor);
            }

            // 6) Send email
            try
            {
                SendRegistrationEmail(professor.Email, professor.GivenName, tempPassword);
            }
            catch (Exception ex)
            {
                try
                {
                    _context.Professors.DeleteOne(p => p.Email == professor.Email && p.GivenName == professor.GivenName && p.LastName == professor.LastName);
                }
                catch { /* log failure */ }

                ViewBag.Error = "Failed to send email. Please check SMTP settings or try again later.";
                return View("Index", professor);
            }

            return View("Success");
        }

        // --- Helper Methods ---

        private static string RegexEscape(string input) => Regex.Escape(input ?? "");

        private static string GenerateTemporaryPassword(int length = 12)
        {
            const string alphanum = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var sb = new System.Text.StringBuilder();
            var rng = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var buffer = new byte[4];

            for (int i = 0; i < length; i++)
            {
                rng.GetBytes(buffer);
                uint num = BitConverter.ToUInt32(buffer, 0);
                sb.Append(alphanum[(int)(num % (uint)alphanum.Length)]);
            }

            return sb.ToString();
        }

        private void SendRegistrationEmail(string toEmail, string givenName, string tempPassword)
        {
            var smtpSection = _config.GetSection("SMTP");
            var host = smtpSection.GetValue<string>("Host");
            var port = smtpSection.GetValue<int>("Port");
            var user = smtpSection.GetValue<string>("Username");
            var pass = smtpSection.GetValue<string>("Password");
            var fromEmail = smtpSection.GetValue<string>("FromEmail") ?? user;
            var fromName = smtpSection.GetValue<string>("FromName") ?? "LMS Subsystem";

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "LMS Account Registered — Temporary Password";

            var body = $@"
<p>Hi {System.Net.WebUtility.HtmlEncode(givenName)},</p>
<p>Your LMS account has been created. Below are your login details (temporary):</p>
<ul>
  <li><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(toEmail)}</li>
  <li><strong>Temporary Password:</strong> {System.Net.WebUtility.HtmlEncode(tempPassword)}</li>
</ul>
<p>This temporary password will expire in 7 days. For security, please <strong>change your password immediately</strong> after logging in.</p>
<p>If you did not request this, please contact the administrator.</p>
";

            message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                client.Connect(host, port, MailKit.Security.SecureSocketOptions.StartTls);
                if (!string.IsNullOrEmpty(user))
                    client.Authenticate(user, pass);
                client.Send(message);
                client.Disconnect(true);
            }
        }

        private async Task<bool> ValidateEmailAsync(string email)
        {
            var apiKey = _config.GetValue<string>("AbstractApi:EmailKey");
            var client = _httpClientFactory.CreateClient();
            var url = $"https://emailreputation.abstractapi.com/v1/?api_key={apiKey}&email={Uri.EscapeDataString(email)}";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Email reputation API failed: {response.StatusCode}");

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var deliverability = doc.RootElement.GetProperty("email_deliverability");
            bool isFormatValid = deliverability.GetProperty("is_format_valid").GetBoolean();
            bool isMxValid = deliverability.GetProperty("is_mx_valid").GetBoolean();
            bool isSmtpValid = deliverability.GetProperty("is_smtp_valid").GetBoolean();

            var quality = doc.RootElement.GetProperty("email_quality");
            bool isDisposable = quality.GetProperty("is_disposable").GetBoolean();
            bool isRole = quality.GetProperty("is_role").GetBoolean();

            // ✅ Accept if format valid + MX exists + not disposable
            //    For Gmail/Outlook/Yahoo, allow smtp=false
            return isFormatValid && isMxValid && !isDisposable && !isRole;
        }
    }
}
