using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using SystemLibrary.Services;
using SystemLibrary.ViewModels;
using Microsoft.AspNetCore.Authorization;

namespace SystemLibrary.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Attempt authentication (handles enrollment fallback, lockout, and password verification)
            var authenticatedUser = await _userService.AuthenticateAsync(model);

            if (authenticatedUser == null)
            {
                // Try to fetch any user (could have been created during auth attempt)
                var user = await _userService.GetUserByEmailAsync(model.Email);
                
                if (user != null && user.LockoutEndTime.HasValue)
                {
                    // Calculate remaining lockout time in seconds for dynamic UI countdown
                    var remaining = user.LockoutEndTime.Value - DateTime.UtcNow;
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }

                    var remainingSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
                    TempData["LockoutSeconds"] = remainingSeconds;
                    // Special marker so the Login view can render a dynamic countdown
                    TempData["ErrorMessage"] = "LOCKED";
                }
                else
                {
                    var remainingAttempts = user != null ? 3 - user.FailedLoginAttempts : 3;
                    TempData["ErrorMessage"] = remainingAttempts < 3
                        ? $"Invalid password. You have {remainingAttempts} attempt(s) left."
                        : "Invalid email or password.";
                }

                return RedirectToAction("Login");
            }

            // Check if user is restricted
            if (authenticatedUser.IsRestricted)
            {
                var restrictionMessage = "Your account has been restricted.";
                if (!string.IsNullOrEmpty(authenticatedUser.RestrictionReason))
                {
                    restrictionMessage += $" Reason: {authenticatedUser.RestrictionReason}";
                }
                TempData["ErrorMessage"] = restrictionMessage;
                return RedirectToAction("Login");
            }

            // Ensure a valid role; coerce unknown roles to 'student' and persist
            var normalizedRole = authenticatedUser.Role?.Trim().ToLower() ?? "";
            if (normalizedRole != "student" && normalizedRole != "librarian" && normalizedRole != "admin")
            {
                normalizedRole = "student";
                authenticatedUser.Role = "student";
                await _userService.UpdateUserAsync(authenticatedUser);
            }

            // Refresh user data to get latest name from enrollment system if needed
            // This ensures FullName is up-to-date, especially after sync
            var refreshedUser = await _userService.GetUserByIdAsync(authenticatedUser._id.ToString());
            if (refreshedUser != null && !string.IsNullOrWhiteSpace(refreshedUser.FullName) && refreshedUser.FullName != authenticatedUser.StudentId)
            {
                authenticatedUser = refreshedUser;
            }

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, authenticatedUser._id.ToString()),
        new Claim(ClaimTypes.Name, authenticatedUser.Username),
        new Claim(ClaimTypes.Role, normalizedRole),
        new Claim("FullName", authenticatedUser.FullName ?? authenticatedUser.StudentId)
    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = false };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Redirect based on role
            return normalizedRole switch
            {
                "student" => RedirectToAction("Dashboard", "Student"),
                "librarian" => RedirectToAction("Dashboard", "Librarian"),
                "admin" => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Login")
            };
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // Forgot Password
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userService.GetUserByUsernameAndEmailAsync(model.Username, model.Email);

            if (user == null)
            {
                TempData["ErrorMessage"] = "No account found with that username and email combination.";
                return RedirectToAction("ForgotPassword");
            }

            // Generate reset token
            var token = await _userService.CreatePasswordResetTokenAsync(user._id.ToString());

            if (token == null)
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return RedirectToAction("ForgotPassword");
            }

            TempData["SuccessMessage"] = "Verification successful! You can now reset your password.";
            TempData["ResetToken"] = token;
            return RedirectToAction("ForgotPassword");
        }

        // Reset Password
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Invalid reset link.";
                return RedirectToAction("Login");
            }

            var resetToken = await _userService.ValidateResetTokenAsync(token);
            if (resetToken == null)
            {
                TempData["ErrorMessage"] = "This reset link has expired or is invalid.";
                return RedirectToAction("Login");
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Token = model.Token;
                TempData["ErrorMessage"] = "Please fix the errors and try again.";
                return View(model);
            }

            var success = await _userService.ResetPasswordWithTokenAsync(model.Token, model.NewPassword);

            if (!success)
            {
                TempData["ErrorMessage"] = "Failed to reset password. The link may have expired.";
                ViewBag.Token = model.Token;
                return View(model);
            }

            TempData["SuccessMessage"] = "Password reset successful!";
            return RedirectToAction("ResetPassword", new { token = model.Token });
        }
    }
}
