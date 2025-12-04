using E_SysV0._01.Models;
using E_SysV0._01.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace E_SysV0._01.Areas.Student.Controllers
{
    [Area("Student")]
    public class EditEnrollmentsController : Controller
    {
        private readonly MongoDBServices _firebase;
        private readonly IWebHostEnvironment _env;

        public EditEnrollmentsController(MongoDBServices firebase, IWebHostEnvironment env)
        {
            _firebase = firebase;
            _env = env;
        }

        private static bool IsRejected(string? status)
            => !string.IsNullOrWhiteSpace(status) && status.StartsWith("Rejected", StringComparison.OrdinalIgnoreCase);

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> EditEnrollment(string token)
        {
            var request = await _firebase.GetEnrollmentRequestByEditTokenAsync(token);
            if (request == null ||
                !IsRejected(request.Status) ||
                request.EditTokenExpires == null ||
                request.EditTokenExpires < DateTime.UtcNow)
            {
                TempData["Error"] = "Link expired or invalid.";
                return RedirectToAction("FreshmenEnrollment", "FreshmenEnrollment");
            }

            if (string.Equals(request.Type, "Transferee", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(EditTransfereeEnrollment), new { token });
            }

            return View("~/Areas/Student/Views/Student/Freshmen/EditEnrollment.cshtml", request);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> EditTransfereeEnrollment(string token)
        {
            var request = await _firebase.GetEnrollmentRequestByEditTokenAsync(token);
            if (request == null ||
                !IsRejected(request.Status) ||
                request.EditTokenExpires == null ||
                request.EditTokenExpires < DateTime.UtcNow ||
                !string.Equals(request.Type, "Transferee", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Link expired, invalid, or not a Transferee request.";
                return RedirectToAction("TransfereeEnrollment", "TransfereeEnrollment");
            }

            return View("~/Areas/Student/Views/Student/Transferee/EditTransfereeEnrollment.cshtml", request);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEnrollment(
            string token,
            [FromForm] Dictionary<string, string>? extraFields)
        {
            var request = await _firebase.GetEnrollmentRequestByEditTokenAsync(token);

            if (request == null ||
                !IsRejected(request.Status) ||
                request.EditTokenExpires == null ||
                request.EditTokenExpires < DateTime.UtcNow)
            {
                TempData["Error"] = "Link expired or invalid.";
                return RedirectToAction("FreshmenEnrollment", "FreshmenEnrollment");
            }

            if (string.Equals(request.Type, "Transferee", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(EditTransfereeEnrollment), new { token });
            }

            try
            {
                string EF(string k) => (extraFields != null && extraFields.TryGetValue(k, out var v)) ? (v ?? "").Trim() : "";

                var errors = new List<string>();

                if (string.IsNullOrWhiteSpace(EF("Student.LastName"))) errors.Add("Student Last Name is required.");
                if (string.IsNullOrWhiteSpace(EF("Student.FirstName"))) errors.Add("Student First Name is required.");
                if (string.IsNullOrWhiteSpace(EF("Student.MiddleName"))) errors.Add("Student Middle Name is required. Type N/A if not applicable.");
                if (string.IsNullOrWhiteSpace(EF("Student.Sex"))) errors.Add("Student Sex is required.");
                if (string.IsNullOrWhiteSpace(EF("Student.ContactNumber"))) errors.Add("Student Contact Number is required.");
                if (string.IsNullOrWhiteSpace(EF("Student.EmailAddress"))) errors.Add("Student Email Address is required.");

                if (string.IsNullOrWhiteSpace(EF("StudentAddress.HouseStreet"))) errors.Add("Student House No. and Street is required.");
                if (string.IsNullOrWhiteSpace(EF("StudentAddress.Barangay"))) errors.Add("Student Barangay is required.");
                if (string.IsNullOrWhiteSpace(EF("StudentAddress.City"))) errors.Add("Student City is required.");
                if (string.IsNullOrWhiteSpace(EF("StudentAddress.PostalCode"))) errors.Add("Student Postal Code is required.");

                if (string.IsNullOrWhiteSpace(EF("Guardian.LastName"))) errors.Add("Parent/Guardian Last Name is required.");
                if (string.IsNullOrWhiteSpace(EF("Guardian.FirstName"))) errors.Add("Parent/Guardian First Name is required.");
                if (string.IsNullOrWhiteSpace(EF("Guardian.Sex"))) errors.Add("Parent/Guardian Sex is required.");
                if (string.IsNullOrWhiteSpace(EF("Guardian.ContactNumber"))) errors.Add("Parent/Guardian Contact Number is required.");
                if (string.IsNullOrWhiteSpace(EF("Guardian.Relationship"))) errors.Add("Parent/Guardian Relationship is required.");

                if (string.IsNullOrWhiteSpace(EF("GuardianAddress.HouseStreet"))) errors.Add("Parent/Guardian House No. and Street is required.");
                if (string.IsNullOrWhiteSpace(EF("GuardianAddress.Barangay"))) errors.Add("Parent/Guardian Barangay is required.");
                if (string.IsNullOrWhiteSpace(EF("GuardianAddress.City"))) errors.Add("Parent/Guardian City is required.");
                if (string.IsNullOrWhiteSpace(EF("GuardianAddress.PostalCode"))) errors.Add("Parent/Guardian Postal Code is required.");

                if (errors.Count > 0)
                {
                    TempData["Error"] = string.Join(" ", errors);
                    return View("~/Areas/Student/Views/Student/Freshmen/EditEnrollment.cshtml", request);
                }

                static string ComposeName(string last, string first, string middle, string? ext)
                {
                    var parts = new[] { first, middle, last, ext }.Where(p => !string.IsNullOrWhiteSpace(p));
                    return System.Text.RegularExpressions.Regex.Replace(string.Join(" ", parts), @"\s+", " ").Trim();
                }

                request.ExtraFields ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in extraFields ?? new())
                {
                    request.ExtraFields[kv.Key] = kv.Value ?? "";
                }

                var fullName = ComposeName(EF("Student.LastName"), EF("Student.FirstName"), EF("Student.MiddleName"), EF("Student.Extension"));
                request.FullName = string.IsNullOrWhiteSpace(fullName) ? request.FullName : fullName;

                var emergencyName = ComposeName(EF("Guardian.LastName"), EF("Guardian.FirstName"), EF("Guardian.MiddleName"), EF("Guardian.Extension"));
                request.EmergencyContactName = string.IsNullOrWhiteSpace(emergencyName) ? request.EmergencyContactName : emergencyName;
                request.EmergencyContactPhone = string.IsNullOrWhiteSpace(EF("Guardian.ContactNumber")) ? request.EmergencyContactPhone : EF("Guardian.ContactNumber");

                var program = EF("Academic.Program");
                if (!string.IsNullOrWhiteSpace(program))
                    request.Program = program;

                string nextStatus;
                var rs = request.Status ?? "";
                if (rs.Contains("1st", StringComparison.OrdinalIgnoreCase))
                    nextStatus = "1st Sem Pending";
                else if (rs.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                    nextStatus = "2nd Sem Pending";
                else
                {
                    request.ExtraFields.TryGetValue("Academic.Semester", out var semVal);
                    nextStatus = (!string.IsNullOrWhiteSpace(semVal) && semVal.StartsWith("2", StringComparison.OrdinalIgnoreCase))
                                 ? "2nd Sem Pending" : "1st Sem Pending";
                }

                request.Status = nextStatus;
                request.Reason = null;
                request.LastUpdatedAt = DateTime.UtcNow;

                request.EditToken = null;
                request.EditTokenExpires = null;

                await _firebase.UpdateEnrollmentRequestAsync(request);

                TempData["Success"] = "Resubmission received. Please log in to track your enrollment status.";
                return RedirectToAction("StudentLogin", "StudentAccount");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Update failed: {ex.Message}";
                return View("~/Areas/Student/Views/Student/Freshmen/EditEnrollment.cshtml", request);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTransfereeEnrollment(
            string token,
            string fullName,
            string emergencyContactName,
            string emergencyContactPhone)
        {
            var request = await _firebase.GetEnrollmentRequestByEditTokenAsync(token);
            if (request == null ||
                !IsRejected(request.Status) ||
                request.EditTokenExpires == null ||
                request.EditTokenExpires < DateTime.UtcNow ||
                !string.Equals(request.Type, "Transferee", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Link expired, invalid, or not a Transferee request.";
                return RedirectToAction("TransfereeEnrollment", "TransfereeEnrollment");
            }

            try
            {
                request.FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName : request.FullName;
                request.EmergencyContactName = !string.IsNullOrWhiteSpace(emergencyContactName) ? emergencyContactName : request.EmergencyContactName;
                request.EmergencyContactPhone = !string.IsNullOrWhiteSpace(emergencyContactPhone) ? emergencyContactPhone : request.EmergencyContactPhone;

                string nextStatus;
                var rs = request.Status ?? "";
                if (rs.Contains("1st", StringComparison.OrdinalIgnoreCase))
                    nextStatus = "1st Sem Pending";
                else if (rs.Contains("2nd", StringComparison.OrdinalIgnoreCase))
                    nextStatus = "2nd Sem Pending";
                else
                    nextStatus = "1st Sem Pending";

                request.Status = nextStatus;
                request.Reason = null;
                request.LastUpdatedAt = DateTime.UtcNow;

                request.EditToken = null;
                request.EditTokenExpires = null;

                await _firebase.UpdateEnrollmentRequestAsync(request);

                TempData["Success"] = "Resubmission received. Please log in to track your enrollment status.";
                return RedirectToAction("StudentLogin", "StudentAccount");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Update failed: {ex.Message}";
                return View("~/Areas/Student/Views/Student/Transferee/EditTransfereeEnrollment.cshtml", request);
            }
        }
    }
}