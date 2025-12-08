using System.ComponentModel.DataAnnotations;

namespace SystemLibrary.ViewModels
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }

        // Optional email to receive notifications; when creating from MOCK, this will be the MAC email
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; }

        // Student-specific fields (only required when role is "student")
        public string? StudentNumber { get; set; }
        public string? YearLevel { get; set; }
        public string? Course { get; set; }
        public string? Program { get; set; }
        public string? Department { get; set; }
        public string? ContactNumber { get; set; }
    }

    public class UpdatePasswordViewModel
    {
        [Required(ErrorMessage = "User ID is required")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Old password is required")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmNewPassword { get; set; }
    }

    public class RestrictAccountRequest
    {
        [Required(ErrorMessage = "User ID is required")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        public string Reason { get; set; }
    }

    public class CreateBookFromMacRequest
    {
        [Required]
        public string ISBN { get; set; }

        [Range(1, int.MaxValue)]
        public int TotalCopies { get; set; }
    }
}